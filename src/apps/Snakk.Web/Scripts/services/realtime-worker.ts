/**
 * Realtime SharedWorker
 *
 * Owns a single SignalR connection shared across all tabs of the same origin.
 * Routes incoming messages to only the tabs that have subscribed to the relevant group.
 * Manages group subscription reference counts — subscribes/unsubscribes from SignalR
 * only when the first/last tab needs a group.
 *
 * Protocol (tab → worker):
 *   { type: 'init', realtimeUrl: string, signalrSrc: string }
 *   { type: 'subscribe', group: string }
 *   { type: 'unsubscribe', group: string }
 *   { type: 'invoke', method: string, args: any[] }
 *
 * Protocol (worker → tab):
 *   { type: 'tab-id', tabId: number }
 *   { type: 'connection-state', state: 'connected' | 'disconnected' | 'reconnecting' }
 *   { type: 'message', event: string, data: any }
 */

/// <reference lib="webworker" />

interface TabPort {
    port: MessagePort;
    subscriptions: Set<string>;
}

// Tab registry
const tabs = new Map<number, TabPort>();
let nextTabId = 0;

// Group → set of subscribed tabIds
const groupSubscribers = new Map<string, Set<number>>();

// Worker init state
let initialized = false;
let realtimeUrl = '';

// SignalR connection (typed as any — loaded via importScripts at runtime)
let connection: any = null;

// Token cache
let cachedToken: string | null = null;
let tokenExpiry = 0;

// ============================================================================
// SharedWorker entry point
// ============================================================================

(self as any).onconnect = function(e: MessageEvent): void {
    const port: MessagePort = e.ports[0]!;
    const tabId = ++nextTabId;
    tabs.set(tabId, { port, subscriptions: new Set() });

    port.onmessage = (event: MessageEvent) => handleMessage(tabId, port, event.data);
    port.start();

    port.postMessage({ type: 'tab-id', tabId });

    // Tell the new tab about current connection state
    const state = connection?.state === 'Connected' ? 'connected'
        : connection ? 'reconnecting'
        : 'disconnected';
    port.postMessage({ type: 'connection-state', state });
};

// ============================================================================
// Message handler
// ============================================================================

async function handleMessage(tabId: number, _port: MessagePort, data: any): Promise<void> {
    switch (data.type) {
        case 'init':
            if (!initialized) {
                // Resolve relative URLs to absolute — SignalR's withUrl() uses window.location
                // which doesn't exist in a SharedWorker context.
                realtimeUrl = new URL(data.realtimeUrl, self.location.href).href;
                initialized = true;
                (self as any).importScripts(data.signalrSrc);
                await connect();
            }
            break;

        case 'subscribe':
            await subscribeTab(tabId, data.group);
            break;

        case 'unsubscribe':
            await unsubscribeTab(tabId, data.group);
            break;

        case 'invoke':
            if (connection?.state === 'Connected') {
                connection.invoke(data.method, ...data.args).catch(() => {});
            }
            break;

        case 'reconnect':
            cachedToken = null;
            if (connection) await connection.stop();
            await connect();
            break;
    }
}

// ============================================================================
// Token management
// ============================================================================

async function getToken(): Promise<string> {
    if (cachedToken && Date.now() < tokenExpiry - 60_000) return cachedToken!;
    const resp = await fetch('/bff/realtime-token', { credentials: 'include' });
    if (!resp.ok) throw new Error('Failed to fetch realtime token');
    const data = await resp.json();
    cachedToken = data.token;
    tokenExpiry = Date.now() + data.expiresInSeconds * 1000;
    // Schedule proactive refresh 5 minutes before expiry
    const refreshIn = data.expiresInSeconds * 1000 - 5 * 60 * 1000;
    if (refreshIn > 0) setTimeout(() => { cachedToken = null; }, refreshIn);
    return cachedToken!;
}

// ============================================================================
// SignalR connection management
// ============================================================================

async function connect(): Promise<void> {
    const signalR = (self as any).signalR;

    connection = new signalR.HubConnectionBuilder()
        .withUrl(realtimeUrl, { accessTokenFactory: getToken })
        .withAutomaticReconnect([0, 2000, 10000, 30000])
        .build();

    // Route all incoming messages by their group field
    connection.on('ReceiveUpdate',          (msg: any) => routeMessage('ReceiveUpdate', msg, msg.group));
    connection.on('ReceiveViewerCount',     (msg: any) => routeMessage('ReceiveViewerCount', msg, msg.group));
    connection.on('ReceiveTyping',          (msg: any) => routeMessage('ReceiveTyping', msg, msg.group));
    // Notification messages are user-specific — broadcast to all tabs (user is auto-subscribed server-side)
    connection.on('ReceiveNotificationCount', (msg: any) => broadcastAll('ReceiveNotificationCount', msg));
    connection.on('ReceiveNotification',      (msg: any) => broadcastAll('ReceiveNotification', msg));

    connection.onreconnected(async () => {
        broadcastState('connected');
        // Re-subscribe all active groups after reconnect
        for (const [group] of groupSubscribers) {
            await invokeSubscribe(group);
        }
    });

    connection.onreconnecting(() => broadcastState('reconnecting'));
    connection.onclose(() => broadcastState('disconnected'));

    try {
        await connection.start();
        await connection.invoke('SubscribeToGlobal').catch(() => {});
        // Replay any group subscriptions that arrived before the connection was ready
        for (const [group] of groupSubscribers) {
            await invokeSubscribe(group);
        }
        broadcastState('connected');
    } catch {
        broadcastState('disconnected');
    }
}

// ============================================================================
// Group subscription management
// ============================================================================

async function subscribeTab(tabId: number, group: string): Promise<void> {
    const tab = tabs.get(tabId);
    if (!tab) return;

    if (tab.subscriptions.has(group)) return; // already subscribed
    tab.subscriptions.add(group);

    if (!groupSubscribers.has(group)) groupSubscribers.set(group, new Set());
    const subs = groupSubscribers.get(group)!;
    subs.add(tabId);

    // First subscriber for this group — tell SignalR
    if (subs.size === 1) await invokeSubscribe(group);
}

async function unsubscribeTab(tabId: number, group: string): Promise<void> {
    const tab = tabs.get(tabId);
    if (!tab) return;

    tab.subscriptions.delete(group);
    groupSubscribers.get(group)?.delete(tabId);

    // Last subscriber left — tell SignalR
    if ((groupSubscribers.get(group)?.size ?? 0) === 0) {
        groupSubscribers.delete(group);
        await invokeUnsubscribe(group);
    }
}

async function invokeSubscribe(group: string): Promise<void> {
    if (connection?.state !== 'Connected') return;
    const [entityType, publicId] = group.split(':');
    switch (entityType) {
        case 'discussion': await connection.invoke('SubscribeToDiscussion', publicId).catch(() => {}); break;
        case 'space':      await connection.invoke('SubscribeToSpace', publicId).catch(() => {}); break;
        case 'hub':        await connection.invoke('SubscribeToHub', publicId).catch(() => {}); break;
    }
}

async function invokeUnsubscribe(group: string): Promise<void> {
    if (connection?.state !== 'Connected') return;
    const [entityType, publicId] = group.split(':');
    switch (entityType) {
        case 'discussion': await connection.invoke('UnsubscribeFromDiscussion', publicId).catch(() => {}); break;
        case 'space':      await connection.invoke('UnsubscribeFromSpace', publicId).catch(() => {}); break;
        case 'hub':        await connection.invoke('UnsubscribeFromHub', publicId).catch(() => {}); break;
    }
}

// ============================================================================
// Message routing helpers
// ============================================================================

function routeMessage(event: string, data: any, group: string | undefined): void {
    if (!group) { broadcastAll(event, data); return; }

    // Global and user messages go to all tabs
    if (group === 'global' || group.startsWith('user:')) {
        broadcastAll(event, data);
        return;
    }

    // Discussion/space/hub messages — only to subscribed tabs
    const subscribers = groupSubscribers.get(group);
    if (!subscribers) return;
    for (const tabId of subscribers) {
        tabs.get(tabId)?.port.postMessage({ type: 'message', event, data });
    }
}

function broadcastAll(event: string, data: any): void {
    for (const [, tab] of tabs) {
        tab.port.postMessage({ type: 'message', event, data });
    }
}

function broadcastState(state: string): void {
    for (const [, tab] of tabs) {
        tab.port.postMessage({ type: 'connection-state', state });
    }
}
