(function () {
    "use strict";

    const selectors = {
        menuToggle: '[data-inc-toggle="menu"]',
        menu: ".inc-dropdown__menu",
        collapseToggle: '[data-inc-toggle="collapse"]',
        tabToggle: '[data-inc-toggle="tab"]',
        themeMode: "[data-inc-theme-mode]:not(html)",
        themeToggle: "[data-inc-theme-toggle]",
        themeSelect: "[data-inc-theme-select]",
        themeLabel: "[data-inc-theme-label]",
        themeSwitcher: "[data-inc-theme-switcher], details.inc-theme-switcher",
        nativeDialogOpen: "[data-inc-native-dialog-open]",
        autoRefresh: "[data-inc-auto-refresh]",
        autoRefreshToggle: '[data-inc-action="auto-refresh-toggle"]',
        modalToggle: '[data-inc-toggle="modal"]',
        modalDismiss: '[data-inc-dismiss="modal"]',
        offcanvasToggle: '[data-inc-toggle="offcanvas"]',
        offcanvasDismiss: '[data-inc-dismiss="offcanvas"]',
        userMenu: ".inc-user-menu",
        tabPane: ".inc-tab-pane",
        modal: ".inc-modal",
        offcanvas: ".inc-offcanvas",
    };

    const focusableSelector = [
        'a[href]',
        'button:not([disabled])',
        'input:not([disabled]):not([type="hidden"])',
        'select:not([disabled])',
        'textarea:not([disabled])',
        '[tabindex]:not([tabindex="-1"])',
    ].join(", ");

    const autoRefreshControllers = [];
    let autoRefreshReloadScheduled = false;
    const themeModes = ["light", "dark", "system"];
    const themeDescriptions = {
        light: "Use the brighter application palette.",
        dark: "Use the darker application palette.",
        system: "Match the device preference automatically.",
    };
    const themeStorageKey = "inc-theme-mode";
    const themeState = {
        mode: "system",
        resolved: "light",
    };
    let themeMediaQuery = null;
    let themeMediaListenerBound = false;
    let themeStorageListenerBound = false;
    let themeInitialized = false;

    function isThemeMode(value) {
        return themeModes.includes(value);
    }

    function getThemeLabel(mode) {
        if (!isThemeMode(mode)) {
            return "System";
        }

        return mode.charAt(0).toUpperCase() + mode.slice(1);
    }

    function getThemeStatusLabel(mode = themeState.mode, resolved = themeState.resolved) {
        return mode === "system"
            ? `${getThemeLabel(mode)} (${getThemeLabel(resolved)})`
            : getThemeLabel(mode);
    }

    function getStoredThemeMode() {
        try {
            const stored = window.localStorage.getItem(themeStorageKey);
            return isThemeMode(stored) ? stored : null;
        } catch {
            return null;
        }
    }

    function getConfiguredThemeMode() {
        const root = document.documentElement;

        return root.getAttribute("data-inc-theme-mode")
            || root.dataset.incThemeMode
            || root.getAttribute("data-bs-theme")
            || "system";
    }

    function persistThemeMode(mode) {
        try {
            if (mode === "system") {
                window.localStorage.removeItem(themeStorageKey);
                return;
            }

            window.localStorage.setItem(themeStorageKey, mode);
        } catch {
            // Ignore storage failures in private mode or restricted contexts.
        }
    }

    function getSystemTheme() {
        if (!window.matchMedia) {
            return "light";
        }

        return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }

    function resolveThemeMode(mode) {
        return mode === "system" ? getSystemTheme() : mode;
    }

    function syncThemeControls(mode, resolved) {
        document.querySelectorAll(selectors.themeMode).forEach((control) => {
            if (!(control instanceof HTMLElement)) {
                return;
            }

            const controlMode = control.getAttribute("data-inc-theme-mode");
            const isSelected = controlMode === mode;
            const role = control.getAttribute("role");

            control.classList.toggle("active", isSelected);
            control.classList.toggle("is-selected", isSelected);

            if (role === "menuitemradio" || role === "radio") {
                control.setAttribute("aria-checked", isSelected ? "true" : "false");
            } else if (control.tagName === "BUTTON" || control.tagName === "A") {
                control.setAttribute("aria-pressed", isSelected ? "true" : "false");
            }

            if (control.tagName === "INPUT" && (control.type === "radio" || control.type === "checkbox")) {
                control.checked = isSelected;
            }

            if (control.tagName === "OPTION") {
                control.selected = isSelected;
            }
        });

        document.querySelectorAll(selectors.themeSelect).forEach((control) => {
            if (control instanceof HTMLSelectElement) {
                control.value = mode;
            }
        });

        document.querySelectorAll(selectors.themeLabel).forEach((label) => {
            if (!(label instanceof HTMLElement)) {
                return;
            }

            const labelType = label.getAttribute("data-inc-theme-label") || "status";

            if (labelType === "resolved") {
                label.textContent = getThemeLabel(resolved);
                return;
            }

            if (labelType === "mode") {
                label.textContent = getThemeLabel(mode);
                return;
            }

            label.textContent = getThemeStatusLabel(mode, resolved);
        });

        document.querySelectorAll(selectors.themeSwitcher).forEach((switcher) => {
            if (!(switcher instanceof HTMLElement)) {
                return;
            }

            switcher.dataset.incThemeModeState = mode;
            switcher.dataset.incThemeResolved = resolved;
        });
    }

    function publishThemeChange() {
        document.documentElement.dispatchEvent(new CustomEvent("inc-theme-change", {
            bubbles: true,
            composed: true,
            detail: {
                mode: themeState.mode,
                resolved: themeState.resolved,
            },
        }));
    }

    function applyThemeMode(mode, options = {}) {
        const nextMode = isThemeMode(mode) ? mode : "system";
        const resolved = resolveThemeMode(nextMode);
        const root = document.documentElement;

        themeState.mode = nextMode;
        themeState.resolved = resolved;

        root.setAttribute("data-inc-theme-mode", nextMode);
        root.setAttribute("data-bs-theme", resolved);
        root.style.colorScheme = resolved;
        root.dataset.incThemeModeState = nextMode;
        root.dataset.incThemeResolved = resolved;

        if (options.persist !== false) {
            persistThemeMode(nextMode);
        }

        if (options.syncControls !== false) {
            syncThemeControls(nextMode, resolved);
        }

        if (options.dispatch !== false) {
            publishThemeChange();
        }

        return themeState;
    }

    function cycleThemeMode() {
        const currentIndex = themeModes.indexOf(themeState.mode);
        const nextMode = themeModes[(currentIndex + 1) % themeModes.length];

        return applyThemeMode(nextMode);
    }

    function createThemeSwitcherOption(mode) {
        const button = document.createElement("button");
        const body = document.createElement("span");
        const label = document.createElement("span");
        const detail = document.createElement("span");

        button.type = "button";
        button.className = "inc-theme-switcher__option";
        button.setAttribute("data-inc-theme-mode", mode);
        button.setAttribute("role", "menuitemradio");

        body.className = "inc-theme-switcher__option-body";
        label.className = "inc-theme-switcher__option-label";
        label.textContent = getThemeLabel(mode);
        detail.className = "inc-theme-switcher__option-detail";
        detail.textContent = themeDescriptions[mode];

        body.append(label, detail);
        button.append(body);

        return button;
    }

    function createThemeSwitcher(options = {}) {
        const switcher = document.createElement("details");
        const summary = document.createElement("summary");
        const meta = document.createElement("span");
        const label = document.createElement("span");
        const status = document.createElement("span");
        const panel = document.createElement("div");
        const header = document.createElement("div");

        switcher.className = "inc-native-menu inc-theme-switcher";

        if (options.variant === "navbar") {
            switcher.classList.add("inc-native-menu--navbar");
        }

        if (options.block) {
            switcher.classList.add("inc-native-menu--block");
        }

        summary.className = "inc-native-menu__summary inc-theme-switcher__summary";
        meta.className = "inc-theme-switcher__meta";
        label.className = "inc-theme-switcher__label";
        label.textContent = options.label || "Theme";
        status.className = "inc-theme-switcher__status";
        status.setAttribute("data-inc-theme-label", "status");
        status.textContent = getThemeStatusLabel();
        meta.append(label, status);
        summary.append(meta);

        panel.className = "inc-native-menu__panel inc-theme-switcher__panel";
        panel.setAttribute("role", "menu");
        panel.setAttribute("aria-label", options.menuLabel || "Theme");

        header.className = "inc-native-menu__header";
        header.textContent = options.heading || "Choose appearance";
        panel.append(header);

        themeModes.forEach((mode) => {
            panel.append(createThemeSwitcherOption(mode));
        });

        switcher.append(summary, panel);
        syncThemeControls(themeState.mode, themeState.resolved);

        return switcher;
    }

    function mountThemeSwitcher(target, options = {}) {
        let host = target;

        if (typeof target === "string") {
            host = document.querySelector(target);
        }

        if (!(host instanceof HTMLElement)) {
            return null;
        }

        const switcher = createThemeSwitcher(options);
        host.replaceChildren(switcher);
        syncThemeControls(themeState.mode, themeState.resolved);

        return switcher;
    }

    function getThemeSwitcherOptions(control) {
        const panel = control.closest(".inc-theme-switcher__panel");

        if (!panel) {
            return [];
        }

        return Array.from(panel.querySelectorAll(selectors.themeMode)).filter((option) => option.closest(".inc-theme-switcher__panel") === panel);
    }

    function focusThemeSwitcherOption(control, direction) {
        const options = getThemeSwitcherOptions(control);

        if (!options.length) {
            return;
        }

        const activeIndex = options.findIndex((option) => option === control);

        if (direction === "first") {
            options[0]?.focus();
            return;
        }

        if (direction === "last") {
            options[options.length - 1]?.focus();
            return;
        }

        const delta = direction === "next" ? 1 : -1;
        const startIndex = activeIndex === -1 ? 0 : activeIndex;
        const nextIndex = (startIndex + delta + options.length) % options.length;
        options[nextIndex]?.focus();
    }

    function initializeThemeSwitchers() {
        document.querySelectorAll(selectors.themeSwitcher).forEach((switcher) => {
            if (!(switcher instanceof HTMLElement) || switcher.dataset.incThemeSwitcherInitialized === "true") {
                return;
            }

            switcher.dataset.incThemeSwitcherInitialized = "true";

            if (switcher.matches("details.inc-theme-switcher")) {
                syncThemeControls(themeState.mode, themeState.resolved);
                return;
            }

            if (switcher.querySelector(selectors.themeMode)) {
                syncThemeControls(themeState.mode, themeState.resolved);
                return;
            }

            mountThemeSwitcher(switcher, {
                variant: switcher.getAttribute("data-inc-theme-switcher-variant")
                    || (switcher.closest(".inc-navbar, .inc-navbar__utilities") ? "navbar" : undefined),
                block: switcher.hasAttribute("data-inc-theme-switcher-block"),
                label: switcher.getAttribute("data-inc-theme-switcher-label") || "Theme",
                menuLabel: switcher.getAttribute("data-inc-theme-switcher-menu-label") || "Theme",
                heading: switcher.getAttribute("data-inc-theme-switcher-heading") || "Choose appearance",
            });
        });
    }

    function bindThemeMediaListener() {
        if (themeMediaListenerBound || !window.matchMedia) {
            return;
        }

        themeMediaListenerBound = true;
        themeMediaQuery = window.matchMedia("(prefers-color-scheme: dark)");

        const handleThemePreferenceChange = () => {
            if (themeState.mode === "system") {
                applyThemeMode("system", { persist: false });
            }
        };

        if (typeof themeMediaQuery.addEventListener === "function") {
            themeMediaQuery.addEventListener("change", handleThemePreferenceChange);
        } else if (typeof themeMediaQuery.addListener === "function") {
            themeMediaQuery.addListener(handleThemePreferenceChange);
        }
    }

    function bindThemeStorageListener() {
        if (themeStorageListenerBound) {
            return;
        }

        themeStorageListenerBound = true;

        window.addEventListener("storage", (event) => {
            if (event.key !== themeStorageKey) {
                return;
            }

            applyThemeMode(getStoredThemeMode() || getConfiguredThemeMode(), {
                persist: false,
            });
        });
    }

    function initializeTheme() {
        if (themeInitialized) {
            syncThemeControls(themeState.mode, themeState.resolved);
            initializeThemeSwitchers();
            return themeState;
        }

        themeInitialized = true;

        applyThemeMode(getStoredThemeMode() || getConfiguredThemeMode(), {
            persist: false,
        });

        bindThemeMediaListener();
        bindThemeStorageListener();
        initializeThemeSwitchers();
        syncThemeControls(themeState.mode, themeState.resolved);

        return themeState;
    }

    applyThemeMode(getStoredThemeMode() || getConfiguredThemeMode(), {
        dispatch: false,
        persist: false,
        syncControls: false,
    });

    function getTarget(trigger) {
        const rawTarget = trigger.getAttribute("data-inc-target")
            || trigger.getAttribute("href")
            || (trigger.getAttribute("aria-controls") ? `#${trigger.getAttribute("aria-controls")}` : "");

        if (!rawTarget || rawTarget === "#") {
            return null;
        }

        try {
            return document.querySelector(rawTarget);
        } catch {
            return null;
        }
    }

    function getFocusableElements(container) {
        if (!container) {
            return [];
        }

        return Array.from(container.querySelectorAll(focusableSelector)).filter((element) => {
            if (!(element instanceof HTMLElement)) {
                return false;
            }

            if (element.hidden || element.getAttribute("aria-hidden") === "true") {
                return false;
            }

            return element.tabIndex >= 0;
        });
    }

    function focusWithin(container, direction = "first") {
        const explicitFocus = container.querySelector("[data-inc-initial-focus]");

        if (explicitFocus instanceof HTMLElement) {
            explicitFocus.focus();
            return true;
        }

        const focusable = getFocusableElements(container);

        if (!focusable.length) {
            if (container instanceof HTMLElement) {
                if (!container.hasAttribute("tabindex")) {
                    container.tabIndex = -1;
                }

                container.focus();
                return true;
            }

            return false;
        }

        if (direction === "last") {
            focusable[focusable.length - 1].focus();
            return true;
        }

        focusable[0].focus();
        return true;
    }

    function rememberTrigger(target, trigger) {
        if (target instanceof HTMLElement && trigger instanceof HTMLElement) {
            target._incReturnFocus = trigger;
        }
    }

    function restoreTriggerFocus(target) {
        if (!(target instanceof HTMLElement)) {
            return;
        }

        const trigger = target._incReturnFocus;

        if (trigger instanceof HTMLElement && document.contains(trigger)) {
            trigger.focus();
        }

        delete target._incReturnFocus;
    }

    function closeMenu(toggle, options = {}) {
        const menu = getTarget(toggle);

        if (!menu) {
            return;
        }

        menu.classList.remove("show");
        toggle.setAttribute("aria-expanded", "false");

        if (options.restoreFocus) {
            toggle.focus();
        }
    }

    function openMenu(toggle, options = {}) {
        const menu = getTarget(toggle);

        if (!menu) {
            return;
        }

        menu.classList.add("show");
        toggle.setAttribute("aria-expanded", "true");

        if (options.focus === "first") {
            const items = getMenuItems(menu);
            items[0]?.focus();
        }

        if (options.focus === "last") {
            const items = getMenuItems(menu);
            items[items.length - 1]?.focus();
        }
    }

    function closeAllMenus(exceptToggle) {
        document.querySelectorAll(selectors.menuToggle).forEach((toggle) => {
            if (exceptToggle && toggle === exceptToggle) {
                return;
            }

            closeMenu(toggle);
        });
    }

    function getMenuItems(menu) {
        return getFocusableElements(menu).filter((item) => item.closest(selectors.menu) === menu);
    }

    function focusMenuItem(menu, direction) {
        const items = getMenuItems(menu);

        if (!items.length) {
            return;
        }

        const activeIndex = items.findIndex((item) => item === document.activeElement);

        if (direction === "first") {
            items[0].focus();
            return;
        }

        if (direction === "last") {
            items[items.length - 1].focus();
            return;
        }

        const delta = direction === "next" ? 1 : -1;
        const startIndex = activeIndex === -1 ? (delta > 0 ? 0 : items.length - 1) : activeIndex;
        const nextIndex = (startIndex + delta + items.length) % items.length;
        items[nextIndex].focus();
    }

    function setCollapseState(trigger, target, expanded) {
        trigger.setAttribute("aria-expanded", expanded ? "true" : "false");
        trigger.classList.toggle("collapsed", !expanded);
        target.classList.toggle("show", expanded);
    }

    function toggleCollapse(trigger) {
        const target = getTarget(trigger);

        if (!target) {
            return;
        }

        const shouldExpand = !target.classList.contains("show");
        const accordionRoot = trigger.closest("[data-inc-accordion]");

        if (accordionRoot && shouldExpand) {
            accordionRoot.querySelectorAll(selectors.collapseToggle).forEach((otherTrigger) => {
                if (otherTrigger === trigger) {
                    return;
                }

                const otherTarget = getTarget(otherTrigger);

                if (otherTarget) {
                    setCollapseState(otherTrigger, otherTarget, false);
                }
            });
        }

        setCollapseState(trigger, target, shouldExpand);
    }

    function getTabList(trigger) {
        return trigger.closest('[role="tablist"], .inc-tabs-nav');
    }

    function getTabsForList(listRoot) {
        return Array.from(listRoot.querySelectorAll(selectors.tabToggle));
    }

    function activateTab(trigger, options = {}) {
        const listRoot = getTabList(trigger);

        if (!listRoot) {
            return;
        }

        const tabs = getTabsForList(listRoot);
        const targetPane = getTarget(trigger);

        if (!targetPane) {
            return;
        }

        tabs.forEach((tab) => {
            const pane = getTarget(tab);
            const isActive = tab === trigger;

            tab.classList.toggle("active", isActive);
            tab.setAttribute("aria-selected", isActive ? "true" : "false");
            tab.tabIndex = isActive ? 0 : -1;

            if (pane) {
                pane.classList.toggle("active", isActive);
                pane.classList.toggle("show", isActive);
                pane.hidden = !isActive;
            }
        });

        if (options.focus && trigger instanceof HTMLElement) {
            trigger.focus();
        }
    }

    function focusTab(trigger, direction) {
        const listRoot = getTabList(trigger);

        if (!listRoot) {
            return;
        }

        const tabs = getTabsForList(listRoot);
        const activeIndex = tabs.findIndex((tab) => tab === trigger);

        if (activeIndex === -1 || !tabs.length) {
            return;
        }

        let nextTab = trigger;

        if (direction === "first") {
            nextTab = tabs[0];
        } else if (direction === "last") {
            nextTab = tabs[tabs.length - 1];
        } else {
            const delta = direction === "next" ? 1 : -1;
            const nextIndex = (activeIndex + delta + tabs.length) % tabs.length;
            nextTab = tabs[nextIndex];
        }

        activateTab(nextTab, { focus: true });
    }

    function syncOverlayBodyState() {
        const hasOpenModal = document.querySelector(`${selectors.modal}.is-open`);
        const hasOpenOffcanvas = document.querySelector(`${selectors.offcanvas}.is-open`);

        document.body.classList.toggle("inc-modal-open", Boolean(hasOpenModal));
        document.body.classList.toggle("inc-offcanvas-open", Boolean(hasOpenOffcanvas));
    }

    function openModal(trigger) {
        const modal = getTarget(trigger);

        if (!modal) {
            return;
        }

        rememberTrigger(modal, trigger);
        modal.hidden = false;
        modal.classList.add("is-open");
        modal.setAttribute("aria-hidden", "false");
        syncOverlayBodyState();
        focusWithin(modal);
    }

    function closeModal(modal, options = {}) {
        if (!modal) {
            return;
        }

        modal.classList.remove("is-open");
        modal.setAttribute("aria-hidden", "true");
        modal.hidden = true;
        syncOverlayBodyState();

        if (options.restoreFocus !== false) {
            restoreTriggerFocus(modal);
        }
    }

    function getOffcanvasBackdrops(target) {
        if (!target.id) {
            return [];
        }

        return Array.from(document.querySelectorAll(`[data-inc-backdrop-for="${target.id}"]`));
    }

    function openOffcanvas(trigger) {
        const panel = getTarget(trigger);

        if (!panel) {
            return;
        }

        rememberTrigger(panel, trigger);
        panel.classList.add("is-open");
        panel.setAttribute("aria-hidden", "false");
        getOffcanvasBackdrops(panel).forEach((backdrop) => {
            backdrop.classList.add("is-open");
            backdrop.hidden = false;
        });
        syncOverlayBodyState();
        focusWithin(panel);
    }

    function openNativeDialog(trigger) {
        const dialogId = trigger.getAttribute("data-inc-native-dialog-open");
        const dialog = dialogId ? document.getElementById(dialogId) : null;

        if (!(dialog instanceof HTMLElement) || dialog.tagName !== "DIALOG" || dialog.open) {
            return;
        }

        if (typeof dialog.showModal === "function") {
            dialog.showModal();
            return;
        }

        if (typeof dialog.show === "function") {
            dialog.show();
        }
    }

    function closeOffcanvas(panel, options = {}) {
        if (!panel) {
            return;
        }

        panel.classList.remove("is-open");
        panel.setAttribute("aria-hidden", "true");
        getOffcanvasBackdrops(panel).forEach((backdrop) => {
            backdrop.classList.remove("is-open");
            backdrop.hidden = true;
        });
        syncOverlayBodyState();

        if (options.restoreFocus !== false) {
            restoreTriggerFocus(panel);
        }
    }

    function getTopOpenOverlay() {
        const overlays = [
            ...document.querySelectorAll(`${selectors.modal}.is-open, ${selectors.offcanvas}.is-open`)
        ];

        return overlays[overlays.length - 1] || null;
    }

    function parsePositiveInteger(value) {
        const parsed = Number.parseInt(value || "", 10);

        if (!Number.isFinite(parsed) || parsed < 1) {
            return null;
        }

        return parsed;
    }

    function formatAutoRefreshRemaining(totalSeconds) {
        if (totalSeconds < 60) {
            return `${totalSeconds}s`;
        }

        const minutes = Math.floor(totalSeconds / 60);
        const seconds = totalSeconds % 60;
        return `${minutes}m ${seconds}s`;
    }

    function getAutoRefreshParts(root) {
        return {
            countdown: root.querySelector(".inc-auto-refresh__countdown"),
            label: root.querySelector(".inc-auto-refresh__label"),
            value: root.querySelector(".inc-auto-refresh__value"),
            status: root.querySelector(".inc-auto-refresh__status"),
            statusText: root.querySelector(".inc-auto-refresh__status-text"),
            toggle: root.querySelector(".inc-auto-refresh__toggle"),
            toggleText: root.querySelector(".inc-auto-refresh__toggle-text"),
        };
    }

    function updateAutoRefreshToggle(controller) {
        const { parts, isPaused, isLoading, pauseActionLabel, resumeActionLabel } = controller;

        if (!(parts.toggle instanceof HTMLElement)) {
            return;
        }

        const actionLabel = isPaused ? resumeActionLabel : pauseActionLabel;
        parts.toggle.disabled = Boolean(isLoading);
        parts.toggle.setAttribute("aria-pressed", isPaused ? "true" : "false");
        parts.toggle.setAttribute("aria-label", actionLabel);

        if (parts.toggleText) {
            parts.toggleText.textContent = actionLabel;
        }
    }

    function renderAutoRefreshCountdown(controller, remainingSeconds) {
        const { root, parts, refreshLabel } = controller;

        if (parts.label) {
            parts.label.textContent = refreshLabel;
        }

        if (parts.value) {
            parts.value.textContent = formatAutoRefreshRemaining(remainingSeconds);
        }

        root.classList.remove("is-paused");
        root.classList.remove("is-loading");
        root.setAttribute("aria-busy", "false");

        if (parts.countdown) {
            parts.countdown.hidden = false;
        }

        if (parts.status) {
            parts.status.hidden = true;
        }

        updateAutoRefreshToggle(controller);
    }

    function renderAutoRefreshPaused(controller, remainingSeconds) {
        const { root, parts, pausedLabel } = controller;

        if (parts.label) {
            parts.label.textContent = pausedLabel;
        }

        if (parts.value) {
            parts.value.textContent = formatAutoRefreshRemaining(remainingSeconds);
        }

        root.classList.add("is-paused");
        root.classList.remove("is-loading");
        root.setAttribute("aria-busy", "false");

        if (parts.countdown) {
            parts.countdown.hidden = false;
        }

        if (parts.status) {
            parts.status.hidden = true;
        }

        updateAutoRefreshToggle(controller);
    }

    function setAutoRefreshLoadingState(controller) {
        const { root, parts, loadingLabel } = controller;

        root.classList.remove("is-paused");
        root.classList.add("is-loading");
        root.setAttribute("aria-busy", "true");

        if (parts.countdown) {
            parts.countdown.hidden = true;
        }

        if (parts.statusText) {
            parts.statusText.textContent = loadingLabel;
        }

        if (parts.status) {
            parts.status.hidden = false;
        }

        updateAutoRefreshToggle(controller);
    }

    function stopAutoRefreshController(controller) {
        if (controller.timeoutId) {
            window.clearTimeout(controller.timeoutId);
            controller.timeoutId = 0;
        }
    }

    function pauseAutoRefresh(controller) {
        if (autoRefreshReloadScheduled || controller.isLoading || controller.isPaused) {
            return;
        }

        controller.isPaused = true;
        controller.remainingMs = Math.max(controller.deadline - Date.now(), 0);
        stopAutoRefreshController(controller);
        renderAutoRefreshPaused(controller, Math.max(1, Math.ceil(controller.remainingMs / 1000)));
    }

    function resumeAutoRefresh(controller) {
        if (autoRefreshReloadScheduled || controller.isLoading || !controller.isPaused) {
            return;
        }

        controller.isPaused = false;
        controller.deadline = Date.now() + controller.remainingMs;
        controller.remainingMs = 0;
        scheduleAutoRefreshTick(controller);
    }

    function toggleAutoRefresh(controller) {
        if (controller.isPaused) {
            resumeAutoRefresh(controller);
            return;
        }

        pauseAutoRefresh(controller);
    }

    function scheduleWindowReload() {
        if (autoRefreshReloadScheduled) {
            return;
        }

        autoRefreshReloadScheduled = true;
        autoRefreshControllers.forEach((controller) => stopAutoRefreshController(controller));

        const deferToPaint = window.requestAnimationFrame
            ? window.requestAnimationFrame.bind(window)
            : (callback) => window.setTimeout(callback, 16);

        deferToPaint(() => {
            window.setTimeout(() => {
                window.location.reload();
            }, 120);
        });
    }

    function startAutoRefreshReload(controller) {
        if (autoRefreshReloadScheduled || controller.isLoading) {
            return;
        }

        controller.isLoading = true;
        stopAutoRefreshController(controller);
        setAutoRefreshLoadingState(controller);
        scheduleWindowReload();
    }

    function scheduleAutoRefreshTick(controller) {
        if (autoRefreshReloadScheduled || controller.isLoading || controller.isPaused) {
            return;
        }

        stopAutoRefreshController(controller);

        const remainingMs = controller.deadline - Date.now();

        if (remainingMs <= 0) {
            startAutoRefreshReload(controller);
            return;
        }

        const remainingSeconds = Math.ceil(remainingMs / 1000);
        renderAutoRefreshCountdown(controller, remainingSeconds);

        const nextDelay = remainingMs % 1000 || 1000;
        controller.timeoutId = window.setTimeout(() => {
            scheduleAutoRefreshTick(controller);
        }, nextDelay);
    }

    function initializeAutoRefresh() {
        document.querySelectorAll(selectors.autoRefresh).forEach((root) => {
            if (!(root instanceof HTMLElement) || root._incAutoRefreshInitialized) {
                return;
            }

            root._incAutoRefreshInitialized = true;

            const refreshSeconds = parsePositiveInteger(root.getAttribute("data-inc-refresh-seconds"));

            if (!refreshSeconds) {
                return;
            }

            const controller = {
                root,
                parts: getAutoRefreshParts(root),
                refreshLabel: root.getAttribute("data-inc-refresh-label") || "Refresh in",
                loadingLabel: root.getAttribute("data-inc-refresh-loading-label") || "Refreshing",
                pausedLabel: root.getAttribute("data-inc-refresh-paused-label") || "Paused at",
                pauseActionLabel: root.getAttribute("data-inc-refresh-pause-action-label") || "Pause",
                resumeActionLabel: root.getAttribute("data-inc-refresh-resume-action-label") || "Resume",
                deadline: Date.now() + (refreshSeconds * 1000),
                remainingMs: refreshSeconds * 1000,
                timeoutId: 0,
                isLoading: false,
                isPaused: false,
            };

            root._incAutoRefreshController = controller;
            autoRefreshControllers.push(controller);
            scheduleAutoRefreshTick(controller);
        });

        if (!document._incAutoRefreshVisibilityBound && autoRefreshControllers.length) {
            document._incAutoRefreshVisibilityBound = true;

            document.addEventListener("visibilitychange", () => {
                if (document.hidden || autoRefreshReloadScheduled) {
                    return;
                }

                autoRefreshControllers.forEach((controller) => {
                    if (controller.isLoading || controller.isPaused) {
                        return;
                    }

                    if ((controller.deadline - Date.now()) <= 0) {
                        startAutoRefreshReload(controller);
                        return;
                    }

                    scheduleAutoRefreshTick(controller);
                });
            });
        }
    }

    function trapFocus(event, container) {
        if (event.key !== "Tab") {
            return false;
        }

        const focusable = getFocusableElements(container);

        if (!focusable.length) {
            event.preventDefault();
            focusWithin(container);
            return true;
        }

        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        const active = document.activeElement;

        if (event.shiftKey && active === first) {
            event.preventDefault();
            last.focus();
            return true;
        }

        if (!event.shiftKey && active === last) {
            event.preventDefault();
            first.focus();
            return true;
        }

        return false;
    }

    function initializeMenus() {
        document.querySelectorAll(selectors.menuToggle).forEach((toggle) => {
            toggle.setAttribute("aria-expanded", "false");

            const menu = getTarget(toggle);

            if (menu?.id) {
                toggle.setAttribute("aria-controls", menu.id);
            }
        });
    }

    function initializeCollapses() {
        document.querySelectorAll(selectors.collapseToggle).forEach((trigger) => {
            const target = getTarget(trigger);

            if (!target) {
                return;
            }

            setCollapseState(trigger, target, target.classList.contains("show"));
        });
    }

    function initializeTabs() {
        document.querySelectorAll(selectors.tabToggle).forEach((tab, index) => {
            const pane = getTarget(tab);
            const isActive = tab.classList.contains("active");

            if (!tab.id) {
                tab.id = `inc-tab-${index + 1}`;
            }

            tab.setAttribute("role", "tab");
            tab.setAttribute("aria-selected", isActive ? "true" : "false");
            tab.tabIndex = isActive ? 0 : -1;

            if (pane) {
                if (pane.id) {
                    tab.setAttribute("aria-controls", pane.id);
                }

                pane.setAttribute("role", "tabpanel");
                pane.setAttribute("aria-labelledby", tab.id);
                pane.hidden = !isActive;
                pane.classList.toggle("show", isActive);
                pane.classList.toggle("active", isActive);
            }
        });

        document.querySelectorAll(selectors.tabPane).forEach((pane) => {
            const hasActiveTab = document.querySelector(`${selectors.tabToggle}[href="#${pane.id}"].active, ${selectors.tabToggle}[data-inc-target="#${pane.id}"].active, ${selectors.tabToggle}[aria-controls="${pane.id}"].active`);
            pane.hidden = !hasActiveTab;
        });
    }

    function attachEventHandlers() {
        document.addEventListener("click", (event) => {
            const themeToggle = event.target.closest(selectors.themeToggle);

            if (themeToggle) {
                event.preventDefault();
                cycleThemeMode();
                return;
            }

            const themeModeControl = event.target.closest(selectors.themeMode);

            if (themeModeControl && themeModeControl.tagName !== "INPUT") {
                event.preventDefault();
                applyThemeMode(themeModeControl.getAttribute("data-inc-theme-mode"));

                const owningSwitcher = themeModeControl.closest("details.inc-theme-switcher");
                const switcherSummary = owningSwitcher?.querySelector("summary");

                if (owningSwitcher instanceof HTMLDetailsElement) {
                    owningSwitcher.open = false;
                }

                if (switcherSummary instanceof HTMLElement) {
                    switcherSummary.focus();
                }

                return;
            }

            const autoRefreshToggle = event.target.closest(selectors.autoRefreshToggle);

            if (autoRefreshToggle) {
                const autoRefreshRoot = autoRefreshToggle.closest(selectors.autoRefresh);
                const controller = autoRefreshRoot?._incAutoRefreshController;

                if (controller) {
                    event.preventDefault();
                    toggleAutoRefresh(controller);
                }

                return;
            }

            const menuToggle = event.target.closest(selectors.menuToggle);

            if (menuToggle) {
                event.preventDefault();

                const menu = getTarget(menuToggle);
                const isOpen = menu ? menu.classList.contains("show") : false;
                closeAllMenus(menuToggle);

                if (!isOpen) {
                    openMenu(menuToggle);
                } else {
                    closeMenu(menuToggle);
                }

                return;
            }

            if (!event.target.closest(selectors.userMenu)) {
                closeAllMenus();
            }

            const collapseToggle = event.target.closest(selectors.collapseToggle);

            if (collapseToggle) {
                event.preventDefault();
                toggleCollapse(collapseToggle);
                return;
            }

            const tabToggle = event.target.closest(selectors.tabToggle);

            if (tabToggle) {
                if (tabToggle.tagName === "A") {
                    event.preventDefault();
                }

                activateTab(tabToggle);
                return;
            }

            const nativeDialogOpen = event.target.closest(selectors.nativeDialogOpen);

            if (nativeDialogOpen) {
                event.preventDefault();
                openNativeDialog(nativeDialogOpen);
                return;
            }

            const modalToggle = event.target.closest(selectors.modalToggle);

            if (modalToggle) {
                event.preventDefault();
                openModal(modalToggle);
                return;
            }

            const modalDismiss = event.target.closest(selectors.modalDismiss);

            if (modalDismiss) {
                event.preventDefault();
                const modal = modalDismiss.closest(selectors.modal) || getTarget(modalDismiss);
                closeModal(modal);
                return;
            }

            const backdropModal = event.target.closest(`${selectors.modal}.is-open`);

            if (backdropModal && event.target.classList.contains("inc-modal__backdrop")) {
                closeModal(backdropModal);
                return;
            }

            const offcanvasToggle = event.target.closest(selectors.offcanvasToggle);

            if (offcanvasToggle) {
                event.preventDefault();
                openOffcanvas(offcanvasToggle);
                return;
            }

            const offcanvasDismiss = event.target.closest(selectors.offcanvasDismiss);

            if (offcanvasDismiss) {
                event.preventDefault();
                const panel = offcanvasDismiss.closest(selectors.offcanvas) || getTarget(offcanvasDismiss);
                closeOffcanvas(panel);
                return;
            }

            const offcanvasBackdrop = event.target.closest("[data-inc-backdrop-for]");

            if (offcanvasBackdrop) {
                const targetId = offcanvasBackdrop.getAttribute("data-inc-backdrop-for");
                const panel = targetId ? document.getElementById(targetId) : null;
                closeOffcanvas(panel);
            }
        });

        document.addEventListener("change", (event) => {
            const themeModeControl = event.target.closest(selectors.themeMode);

            if (themeModeControl) {
                applyThemeMode(themeModeControl.getAttribute("data-inc-theme-mode"));
                return;
            }

            const themeSelect = event.target.closest(selectors.themeSelect);

            if (themeSelect) {
                applyThemeMode(themeSelect.value);
            }
        });

        document.addEventListener("keydown", (event) => {
            const menuToggle = event.target.closest(selectors.menuToggle);
            const menu = event.target.closest(selectors.menu);
            const tabToggle = event.target.closest(selectors.tabToggle);
            const themeModeControl = event.target.closest(selectors.themeMode);
            const openOverlay = getTopOpenOverlay();

            if (themeModeControl && themeModeControl.closest(".inc-theme-switcher__panel")) {
                if (event.key === "ArrowDown") {
                    event.preventDefault();
                    focusThemeSwitcherOption(themeModeControl, "next");
                    return;
                }

                if (event.key === "ArrowUp") {
                    event.preventDefault();
                    focusThemeSwitcherOption(themeModeControl, "previous");
                    return;
                }

                if (event.key === "Home") {
                    event.preventDefault();
                    focusThemeSwitcherOption(themeModeControl, "first");
                    return;
                }

                if (event.key === "End") {
                    event.preventDefault();
                    focusThemeSwitcherOption(themeModeControl, "last");
                    return;
                }

                if (event.key === "Escape") {
                    const owningSwitcher = themeModeControl.closest("details.inc-theme-switcher");
                    const switcherSummary = owningSwitcher?.querySelector("summary");

                    if (owningSwitcher instanceof HTMLDetailsElement) {
                        owningSwitcher.open = false;
                    }

                    if (switcherSummary instanceof HTMLElement) {
                        switcherSummary.focus();
                    }

                    return;
                }
            }

            if (menuToggle) {
                if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                    event.preventDefault();
                    closeAllMenus(menuToggle);
                    openMenu(menuToggle, { focus: event.key === "ArrowDown" ? "first" : "last" });
                    return;
                }

                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    const isExpanded = menuToggle.getAttribute("aria-expanded") === "true";

                    if (isExpanded) {
                        closeMenu(menuToggle);
                    } else {
                        closeAllMenus(menuToggle);
                        openMenu(menuToggle, { focus: "first" });
                    }

                    return;
                }
            }

            if (menu) {
                if (event.key === "ArrowDown") {
                    event.preventDefault();
                    focusMenuItem(menu, "next");
                    return;
                }

                if (event.key === "ArrowUp") {
                    event.preventDefault();
                    focusMenuItem(menu, "previous");
                    return;
                }

                if (event.key === "Home") {
                    event.preventDefault();
                    focusMenuItem(menu, "first");
                    return;
                }

                if (event.key === "End") {
                    event.preventDefault();
                    focusMenuItem(menu, "last");
                    return;
                }

                if (event.key === "Escape") {
                    event.preventDefault();
                    const owningToggle = document.querySelector(`${selectors.menuToggle}[aria-controls="${menu.id}"]`);

                    if (owningToggle) {
                        closeMenu(owningToggle, { restoreFocus: true });
                    }

                    return;
                }
            }

            if (tabToggle) {
                if (event.key === "ArrowRight" || event.key === "ArrowDown") {
                    event.preventDefault();
                    focusTab(tabToggle, "next");
                    return;
                }

                if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
                    event.preventDefault();
                    focusTab(tabToggle, "previous");
                    return;
                }

                if (event.key === "Home") {
                    event.preventDefault();
                    focusTab(tabToggle, "first");
                    return;
                }

                if (event.key === "End") {
                    event.preventDefault();
                    focusTab(tabToggle, "last");
                    return;
                }

                if ((event.key === "Enter" || event.key === " ") && tabToggle.tagName !== "BUTTON") {
                    event.preventDefault();
                    activateTab(tabToggle, { focus: true });
                    return;
                }
            }

            if (openOverlay && trapFocus(event, openOverlay)) {
                return;
            }

            if (event.key === "Escape") {
                const openModal = document.querySelector(`${selectors.modal}.is-open`);
                const openPanel = document.querySelector(`${selectors.offcanvas}.is-open`);

                if (openModal) {
                    closeModal(openModal);
                    return;
                }

                if (openPanel) {
                    closeOffcanvas(openPanel);
                    return;
                }

                closeAllMenus();
            }
        });
    }

    window.IncTheme = {
        getMode() {
            return themeState.mode;
        },
        getResolvedTheme() {
            return themeState.resolved;
        },
        setMode(mode) {
            return applyThemeMode(mode);
        },
        cycleMode() {
            return cycleThemeMode();
        },
        createSwitcher(options = {}) {
            return createThemeSwitcher(options);
        },
        mountSwitcher(target, options = {}) {
            return mountThemeSwitcher(target, options);
        },
        init() {
            return initializeTheme();
        },
    };

    function initialize() {
        initializeTheme();
        initializeMenus();
        initializeCollapses();
        initializeTabs();
        initializeAutoRefresh();
        attachEventHandlers();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize, { once: true });
    } else {
        initialize();
    }
})();
