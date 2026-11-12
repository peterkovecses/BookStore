window.bookStoreVerificationLink = (() => {
    const path = window.location.pathname.replace(/\/+$/, "").toLowerCase();
    const isVerificationPage = path === "/verify-email";
    const isPasswordResetPage = path === "/reset-password";
    const isSensitiveLinkPage = isVerificationPage || isPasswordResetPage;
    const readLink = () => {
        const parameters = new URLSearchParams(window.location.search);
        const users = parameters.getAll("userId");
        const tokens = parameters.getAll("token");
        return users.length === 1 && tokens.length === 1
            ? { userId: users[0], token: tokens[0] }
            : null;
    };
    let verificationLink = isVerificationPage ? readLink() : null;
    let passwordResetLink = isPasswordResetPage ? readLink() : null;
    if (isSensitiveLinkPage) {
        window.history.replaceState(window.history.state, "", window.location.pathname);
    }

    return {
        isVerificationPage,
        isSensitiveLinkPage,
        take: () => {
            const result = verificationLink;
            verificationLink = null;
            return result;
        },
        takePasswordReset: () => {
            const result = passwordResetLink;
            passwordResetLink = null;
            return result;
        }
    };
})();
