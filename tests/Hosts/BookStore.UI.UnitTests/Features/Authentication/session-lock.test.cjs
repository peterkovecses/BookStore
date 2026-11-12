const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");

const source = fs.readFileSync(path.join(__dirname, "../../../../../src/Hosts/BookStore.UI/wwwroot/js/session-lock.js"), "utf8");

function createTab(locks) {
    const context = { window: {}, navigator: { locks }, AbortController, setTimeout, clearTimeout };
    vm.runInNewContext(source, context);
    return context.window.bookStoreSessionLock;
}

test("serializes cookie operations across tabs", async () => {
    let tail = Promise.resolve();
    const locks = {
        request: (_name, _options, callback) => {
            const next = tail.then(callback);
            tail = next;
            return next;
        }
    };
    const first = createTab(locks);
    const second = createTab(locks);
    await first.acquire();
    let acquired = false;
    const pending = second.acquire().then(() => { acquired = true; });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(acquired, false);
    first.release();
    await pending;
    assert.equal(acquired, true);
    second.release();
});

test("fails closed when Web Locks are unavailable", async () => {
    const tab = createTab(undefined);
    await assert.rejects(tab.acquire(), /Web Locks are required/);
});
