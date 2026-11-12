window.bookStoreVerificationLink = (() => {
    const isVerificationPage = window.location.pathname.replace(/\/+$/, "").toLowerCase() === "/verify-email";
    let link = null;
    if (isVerificationPage) {
        const parameters = new URLSearchParams(window.location.search);
        const users = parameters.getAll("userId");
        const tokens = parameters.getAll("token");
        if (users.length === 1 && tokens.length === 1) {
            link = { userId: users[0], token: tokens[0] };
        }
        window.history.replaceState(window.history.state, "", window.location.pathname);
    }

    return {
        isVerificationPage,
        take: () => {
            const result = link;
            link = null;
            return result;
        }
    };
})();
