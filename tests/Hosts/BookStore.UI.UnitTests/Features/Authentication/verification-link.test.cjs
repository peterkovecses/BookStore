const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const path = require("node:path");

const source = fs.readFileSync(path.join(__dirname, "../../../../../src/Hosts/BookStore.UI/wwwroot/js/verification-link.js"), "utf8");

function open(pathname, search) {
    let replaced;
    const window = {
        location: { pathname, search },
        history: { state: null, replaceState: (_state, _title, url) => { replaced = url; } }
    };
    vm.runInNewContext(source, { window, URLSearchParams });
    return { link: window.bookStoreVerificationLink, replaced };
}

test("strips the query and consumes safely encoded link exactly once", () => {
    const page = open("/verify-email", "?userId=user%2D1&token=abc%5Fdef%2D123");
    const value = page.link.take();
    assert.equal(page.replaced, "/verify-email");
    assert.equal(value.userId, "user-1");
    assert.equal(value.token, "abc_def-123");
    assert.equal(page.link.take(), null);
});

test("rejects duplicated and missing link parameters", () => {
    for (const search of ["?userId=one&userId=two&token=abc", "?userId=one&token=a&token=b", "?userId=one"]) {
        const page = open("/verify-email", search);
        assert.equal(page.replaced, "/verify-email");
        assert.equal(page.link.take(), null);
    }
});

test("strips and consumes password reset tokens from the URL", () => {
    const page = open("/reset-password", "?userId=user%2D1&token=abc%5Fdef%2D123");
    const value = page.link.takePasswordReset();
    assert.equal(page.replaced, "/reset-password");
    assert.equal(value.userId, "user-1");
    assert.equal(value.token, "abc_def-123");
    assert.equal(page.link.takePasswordReset(), null);
});

test("rejects duplicated reset link parameters", () => {
    const page = open("/reset-password", "?userId=one&token=a&token=b");
    assert.equal(page.replaced, "/reset-password");
    assert.equal(page.link.takePasswordReset(), null);
});

test("leaves catalog navigation unchanged", () => {
    const page = open("/books", "?searchTerm=book");
    assert.equal(page.link.isVerificationPage, false);
    assert.equal(page.link.isSensitiveLinkPage, false);
    assert.equal(page.replaced, undefined);
});
