/**
 * Generic Cache Manager with TTL and LRU eviction
 */

// ============================================================================
// Type Definitions
// ============================================================================

interface CacheEntry<T> {
    data: T;
    cachedAt: number;
    lastAccessedAt: number;
}

interface SnakkCacheStorage<T> {
    [key: string]: CacheEntry<T>;
}


// ============================================================================
// Implementation
// ============================================================================

class CacheManager<T = any> {
    private storageKey: string;
    private ttl: number;
    private maxItems: number;

    constructor(storageKey: string, ttlMinutes: number = 5, maxItems: number = 50) {
        this.storageKey = storageKey;
        this.ttl = ttlMinutes * 60 * 1000;
        this.maxItems = maxItems;
    }

    /**
     * Get a cached item by ID
     */
    get(id: string): T | null {
        const cache = this.getAll();
        const item = cache[id];
        if (!item) return null;

        // Check expiration
        if (Date.now() - item.cachedAt > this.ttl) {
            this.remove(id);
            return null;
        }

        // Update access time for LRU
        item.lastAccessedAt = Date.now();
        this._saveCache(cache);

        return item.data;
    }

    /**
     * Set a cached item
     */
    set(id: string, data: T): void {
        const cache = this.getAll();
        cache[id] = {
            data,
            cachedAt: Date.now(),
            lastAccessedAt: Date.now()
        };

        // LRU eviction if over limit
        const entries = Object.entries(cache);
        if (entries.length > this.maxItems) {
            // Sort by last accessed time (oldest first)
            entries.sort((a, b) => {
                const entryA = a[1] as CacheEntry<T>;
                const entryB = b[1] as CacheEntry<T>;
                return entryA.lastAccessedAt - entryB.lastAccessedAt;
            });
            const toRemove = entries.slice(0, entries.length - this.maxItems);
            toRemove.forEach(([key]) => delete cache[key]);
        }

        this._saveCache(cache);
    }

    /**
     * Get all cached items (including expired)
     */
    getAll(): SnakkCacheStorage<T> {
        try {
            const stored = localStorage.getItem(this.storageKey);
            return JSON.parse(stored || '{}') as SnakkCacheStorage<T>;
        } catch (e) {
            console.error('Failed to load cache:', e);
            return {} as SnakkCacheStorage<T>;
        }
    }

    /**
     * Get all valid (non-expired) items
     */
    getAllValid(): Record<string, T> {
        const cache = this.getAll();
        const valid: Record<string, T> = {};
        const now = Date.now();

        for (const [id, item] of Object.entries(cache)) {
            if (now - item.cachedAt <= this.ttl) {
                valid[id] = item.data;
            }
        }

        return valid;
    }

    /**
     * Remove a specific item
     */
    remove(id: string): void {
        const cache = this.getAll();
        delete cache[id];
        this._saveCache(cache);
    }

    /**
     * Clear all cached items
     */
    clear(): void {
        localStorage.removeItem(this.storageKey);
    }

    /**
     * Remove expired items
     */
    pruneExpired(): void {
        const cache = this.getAll();
        const now = Date.now();
        let changed = false;

        for (const [id, item] of Object.entries(cache)) {
            if (now - item.cachedAt > this.ttl) {
                delete cache[id];
                changed = true;
            }
        }

        if (changed) {
            this._saveCache(cache);
        }
    }

    /**
     * Check if an item exists and is valid
     */
    has(id: string): boolean {
        return this.get(id) !== null;
    }

    /**
     * Get cache age in milliseconds
     */
    getAge(id: string): number | null {
        const cache = this.getAll();
        const item = cache[id];
        if (!item) return null;
        return Date.now() - item.cachedAt;
    }

    /**
     * Internal: Save cache to localStorage
     */
    private _saveCache(cache: SnakkCacheStorage<T>): void {
        try {
            localStorage.setItem(this.storageKey, JSON.stringify(cache));
        } catch (e) {
            console.error('Failed to save cache:', e);
        }
    }
}

// Export for use in other modules
(window as any).CacheManager = CacheManager;
