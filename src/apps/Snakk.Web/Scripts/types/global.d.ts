// Global type definitions for Snakk platform

/// <reference lib="dom" />

// ============================================================================
// SignalR Types
// ============================================================================

declare namespace signalR {
    export interface HubConnectionBuilder {
        withUrl(url: string, options?: any): HubConnectionBuilder;
        withAutomaticReconnect(retryDelays?: number[]): HubConnectionBuilder;
        configureLogging(logLevel: any): HubConnectionBuilder;
        build(): HubConnection;
    }

    export interface HubConnection {
        start(): Promise<void>;
        stop(): Promise<void>;
        on(methodName: string, callback: (...args: any[]) => void): void;
        off(methodName: string, callback?: (...args: any[]) => void): void;
        invoke(methodName: string, ...args: any[]): Promise<any>;
        onclose(callback: (error?: Error) => void): void;
        onreconnecting(callback: (error?: Error) => void): void;
        onreconnected(callback: (connectionId?: string) => void): void;
        state: HubConnectionState;
    }

    export enum HubConnectionState {
        Disconnected = "Disconnected",
        Connecting = "Connecting",
        Connected = "Connected",
        Disconnecting = "Disconnecting",
        Reconnecting = "Reconnecting"
    }

    export enum LogLevel {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        None = 6
    }

    export const HubConnectionBuilder: new () => HubConnectionBuilder;
}

// ============================================================================
// HTMX Types
// ============================================================================

declare namespace htmx {
    export interface Config {
        historyEnabled: boolean;
        historyCacheSize: number;
        refreshOnHistoryMiss: boolean;
        defaultSwapStyle: string;
        defaultSwapDelay: number;
        defaultSettleDelay: number;
        includeIndicatorStyles: boolean;
        indicatorClass: string;
        requestClass: string;
        addedClass: string;
        settlingClass: string;
        swappingClass: string;
        allowEval: boolean;
        allowScriptTags: boolean;
        inlineScriptNonce: string;
        attributesToSettle: string[];
        withCredentials: boolean;
        timeout: number;
        wsReconnectDelay: string;
        wsBinaryType: string;
        disableSelector: string;
    }

    export function process(element: Element): void;
    export function ajax(method: string, path: string, selector: string | Element): void;
    export function trigger(element: Element, name: string, detail?: any): boolean;
    export function on(target: string | Element, event: string, listener: (evt: CustomEvent) => void): void;
    export function off(target: string | Element, event: string, listener?: (evt: CustomEvent) => void): void;
    export function find(selector: string): Element | null;
    export function findAll(selector: string): NodeListOf<Element>;
    export function closest(element: Element, selector: string): Element | null;
}

declare const htmx: typeof htmx;

// ============================================================================
// Window Interface Extensions
// ============================================================================

interface SnakkConfig {
    communityPrefix: string;
    showCommunityInDiscussionList: boolean;
    discussions?: {
        nextCursor: string;
        hasMoreItems: boolean;
        pageSize: number;
        communityId?: string;
    };
    space?: {
        publicId: string;
        slug: string;
        hubSlug: string;
        initialDiscussionsCount: number;
        hasMoreItems: boolean;
        pageSize: number;
    };
}

interface SnakkActionsAPI {
    on(action: string, handler: (el: HTMLElement, event: Event) => void): void;
    onAll(handlers: Record<string, (el: HTMLElement, event: Event) => void>): void;
}

interface Window {
    // Action delegation
    SnakkActions: SnakkActionsAPI;

    // Snakk modules
    SnakkAuth: any;
    SnakkUtils: any;
    snakkTheme: any;
    SnakkRealtime: any;
    SnakkPopup: any;
    SnakkPopupInstance: any;

    // User role types enum
    UserRoleType: any;

    // Components
    SnakkLightbox: { open(urls: string[], startIdx: number, blurs?: (string | null)[]): void };

    // Service modules
    CacheManager: any;
    DraftManager: any;
    SnakkDraftManager: any;
    SnakkFollowCache: any;
    SnakkReadHistory: any;
    ReadStateBatcher: any;
    SnakkReadStateBatcher: any;
    SnakkSearchHistory: any;

    // Runtime configuration
    realtimeServiceUrl?: string;
    currentUserId?: string;
    snakkTimezone?: string;
    snakkRealtime: any;

    // Page-specific exports
    SnakkConfig: SnakkConfig;
    FrontpageDiscussions: any;
    SnakkFrontpageDiscussions: any;
    // SignalR
    signalR: typeof signalR;

    // HTMX
    htmx: typeof htmx;
}

// ============================================================================
// Custom Events
// ============================================================================

interface SnakkAuthTokenSetEvent extends CustomEvent {
    detail: {
        userId: string;
        displayName: string;
    };
}

interface SnakkAuthTokenClearedEvent extends CustomEvent {
    detail: Record<string, never>;
}

interface SnakkNavLoadedEvent extends CustomEvent {
    detail: Record<string, never>;
}

interface DocumentEventMap {
    'snakk:auth:token-set': SnakkAuthTokenSetEvent;
    'snakk:auth:token-cleared': SnakkAuthTokenClearedEvent;
    'snakk:nav:loaded': SnakkNavLoadedEvent;
}

// ============================================================================
// Common Data Structures
// ============================================================================

interface AuthHeaders {
    'Authorization': string;
    'X-User-Id': string;
}

interface TokenData {
    token: string;
    userId: string;
    displayName: string;
    expiresAt: number;
}

interface UserInfo {
    id: string;
    displayName: string;
    email?: string;
    avatarUrl?: string;
    roles: string[];
}

// ============================================================================
// API Response Types
// ============================================================================

interface ApiResponse<T = any> {
    success: boolean;
    data?: T;
    error?: string;
    message?: string;
}

interface PaginatedResponse<T> {
    items: T[];
    total: number;
    page: number;
    pageSize: number;
}

// ============================================================================
// Utility Types
// ============================================================================

type Nullable<T> = T | null;
type Optional<T> = T | undefined;
type MaybePromise<T> = T | Promise<T>;
