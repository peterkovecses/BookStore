window.bookStoreSessionLock = (() => {
    let release;

    return {
        acquire: () => {
            if (!navigator.locks) {
                return Promise.reject(new Error("Web Locks are required for secure session coordination."));
            }

            return new Promise((resolve, reject) => {
                const controller = new AbortController();
                const timeout = setTimeout(() => controller.abort(), 15000);
                navigator.locks.request("bookstore-identity-session", { signal: controller.signal }, async () => {
                    clearTimeout(timeout);
                    await new Promise(done => {
                        release = done;
                        resolve();
                    });
                    release = undefined;
                }).catch(error => {
                    clearTimeout(timeout);
                    reject(error);
                });
            });
        },
        release: () => {
            if (!release) {
                throw new Error("Session lock is not held.");
            }
            release();
        }
    };
})();
