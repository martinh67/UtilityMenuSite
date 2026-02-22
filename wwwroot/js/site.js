// UtilityMenu Site — site.js
// Client-side utilities for Blazor interop

'use strict';

// ── Clipboard ─────────────────────────────────────────────────
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        // Fallback for older browsers / non-HTTPS
        try {
            const el = document.createElement('textarea');
            el.value = text;
            el.style.position = 'fixed';
            el.style.opacity = '0';
            document.body.appendChild(el);
            el.focus();
            el.select();
            const ok = document.execCommand('copy');
            document.body.removeChild(el);
            return ok;
        } catch {
            return false;
        }
    }
};

// ── Scroll Utilities ─────────────────────────────────────────
window.scrollToTop = function () {
    window.scrollTo({ top: 0, behavior: 'smooth' });
};

window.scrollToElement = function (elementId) {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};

// ── Bootstrap Tooltip / Popover init ─────────────────────────
function initBootstrapComponents() {
    // Tooltips
    const tooltipEls = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipEls.forEach(el => {
        if (!el._bsTooltip) {
            el._bsTooltip = new bootstrap.Tooltip(el);
        }
    });
    // Popovers
    const popoverEls = document.querySelectorAll('[data-bs-toggle="popover"]');
    popoverEls.forEach(el => {
        if (!el._bsPopover) {
            el._bsPopover = new bootstrap.Popover(el);
        }
    });
}

// Re-init after each Blazor enhanced navigation
document.addEventListener('DOMContentLoaded', initBootstrapComponents);
document.addEventListener('blazor:navigated', initBootstrapComponents);

// ── Sidebar Toggle (mobile) ───────────────────────────────────
window.toggleSidebar = function (sidebarId) {
    const sidebar = document.getElementById(sidebarId);
    if (sidebar) sidebar.classList.toggle('open');
};

window.closeSidebar = function (sidebarId) {
    const sidebar = document.getElementById(sidebarId);
    if (sidebar) sidebar.classList.remove('open');
};

// ── Focus Management ──────────────────────────────────────────
window.focusElement = function (selector) {
    const el = document.querySelector(selector);
    if (el) el.focus();
};

// ── Local Storage helpers (for Blazor interop) ───────────────
window.localStorageHelper = {
    getItem: function (key) {
        return localStorage.getItem(key);
    },
    setItem: function (key, value) {
        localStorage.setItem(key, value);
    },
    removeItem: function (key) {
        localStorage.removeItem(key);
    }
};

// ── Page Title ────────────────────────────────────────────────
window.setDocumentTitle = function (title) {
    document.title = title;
};
