/**
 * Generic Cache Manager with TTL and LRU eviction
 */
class CacheManager {
    constructor(storageKey, ttlMinutes = 5, maxItems = 50) {
        this.storageKey = storageKey;
        this.ttl = ttlMinutes * 60 * 1000;
        this.maxItems = maxItems;
    }

    /**
     * Get a cached item by ID
     * @param {string} id
     * @returns {any|null}
     */
    get(id) {
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
     * @param {string} id
     * @param {any} data
     */
    set(id, data) {
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
            entries.sort((a, b) => a[1].lastAccessedAt - b[1].lastAccessedAt);
            const toRemove = entries.slice(0, entries.length - this.maxItems);
            toRemove.forEach(([key]) => delete cache[key]);
        }

        this._saveCache(cache);
    }

    /**
     * Get all cached items (including expired)
     * @returns {Object}
     */
    getAll() {
        try {
            return JSON.parse(localStorage.getItem(this.storageKey) || '{}');
        } catch (e) {
            console.error('Failed to load cache:', e);
            return {};
        }
    }

    /**
     * Get all valid (non-expired) items
     * @returns {Object}
     */
    getAllValid() {
        const cache = this.getAll();
        const valid = {};
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
     * @param {string} id
     */
    remove(id) {
        const cache = this.getAll();
        delete cache[id];
        this._saveCache(cache);
    }

    /**
     * Clear all cached items
     */
    clear() {
        localStorage.removeItem(this.storageKey);
    }

    /**
     * Remove expired items
     */
    pruneExpired() {
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
     * @param {string} id
     * @returns {boolean}
     */
    has(id) {
        return this.get(id) !== null;
    }

    /**
     * Get cache age in milliseconds
     * @param {string} id
     * @returns {number|null}
     */
    getAge(id) {
        const cache = this.getAll();
        const item = cache[id];
        if (!item) return null;
        return Date.now() - item.cachedAt;
    }

    /**
     * Internal: Save cache to localStorage
     * @private
     */
    _saveCache(cache) {
        try {
            localStorage.setItem(this.storageKey, JSON.stringify(cache));
        } catch (e) {
            console.error('Failed to save cache:', e);
        }
    }
}

// Export for use in other modules
window.CacheManager = CacheManager;
