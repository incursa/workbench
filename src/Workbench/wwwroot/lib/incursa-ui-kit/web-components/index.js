const FALSE_TOKENS = new Set(["false", "0", "off", "no"]);
const THEME_MODES = ["light", "dark", "system"];

function toBoolean(value, fallback = false) {
    if (value == null) return fallback;
    return !FALSE_TOKENS.has(String(value).toLowerCase());
}

function byClass(host, className) {
    return Array.from(host.children).find((node) => node.classList?.contains(className)) || null;
}

function addClass(node, className) {
    if (node instanceof Element && className) node.classList.add(className);
}

function emit(host, type, detail = {}, options = {}) {
    return host.dispatchEvent(new CustomEvent(type, {
        detail,
        bubbles: options.bubbles !== false,
        composed: options.composed !== false,
        cancelable: options.cancelable === true,
    }));
}

function moveSlots(host, mapping) {
    Object.entries(mapping).forEach(([slotName, className]) => {
        Array.from(host.children)
            .filter((node) => node.getAttribute("slot") === slotName)
            .forEach((node) => {
                node.removeAttribute("slot");
                addClass(node, className);
            });
    });
}

function ensureNode(parent, selector, build) {
    const existing = parent.querySelector(`:scope > ${selector}`);
    if (existing) return existing;
    const node = build();
    parent.append(node);
    return node;
}

class IncElement extends HTMLElement {
    emit(type, detail = {}, options = {}) {
        return emit(this, type, detail, options);
    }
}

class IncAppShellElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-app-shell");
        moveSlots(this, {
            header: "inc-app-shell__header",
            body: "inc-app-shell__body",
            sidebar: "inc-app-shell__sidebar",
            main: "inc-app-shell__main",
            content: "inc-app-shell__content",
            footer: "inc-app-shell__footer",
        });
    }
}

class IncPageElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-page");
        moveSlots(this, {
            breadcrumbs: "inc-page__breadcrumbs",
            header: "inc-page__header",
            body: "inc-page__body",
            aside: "inc-page__aside",
            footer: "inc-page__footer",
        });
    }
}

class IncPageHeaderElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-page-header");
        moveSlots(this, { title: "inc-page-header__title", body: "inc-page-header__body", actions: "inc-page-header__actions" });
    }
}

class IncSectionElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-section-container");
        addClass(this, "inc-section");
        moveSlots(this, {
            header: "inc-section__header",
            body: "inc-section__body",
            footer: "inc-section__footer",
            actions: "inc-section__actions",
        });
    }
}

class IncCardElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-card");
        moveSlots(this, { header: "inc-card__header", body: "inc-card__body", footer: "inc-card__footer" });
    }
}

class IncSummaryOverviewElement extends IncElement {
    static get observedAttributes() { return ["columns"]; }
    connectedCallback() {
        addClass(this, "inc-summary-overview");
        this.syncColumns();
    }
    attributeChangedCallback() { this.syncColumns(); }
    syncColumns() {
        this.classList.remove("inc-summary-overview--2-col", "inc-summary-overview--3-col", "inc-summary-overview--4-col");
        const columns = Number.parseInt(this.getAttribute("columns") || "", 10);
        if ([2, 3, 4].includes(columns)) this.classList.add(`inc-summary-overview--${columns}-col`);
    }
}

class IncSummaryBlockElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-summary-block");
        moveSlots(this, {
            header: "inc-summary-block__header",
            body: "inc-summary-block__body",
            value: "inc-summary-block__value",
            status: "inc-summary-block__status",
            actions: "inc-summary-block__actions",
            footer: "inc-summary-block__footer",
        });
    }
}

class IncFooterBarElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-footer-bar");
        moveSlots(this, { menu: "inc-footer-bar__menu", meta: "inc-footer-bar__meta" });
    }
}

class IncNavbarElement extends IncElement {
    static get observedAttributes() { return ["open", "app", "breakpoint", "variant"]; }
    connectedCallback() {
        addClass(this, "inc-navbar");
        this.syncSlots();
        this.syncState();
        this.addEventListener("click", this.onClick);
    }
    disconnectedCallback() { this.removeEventListener("click", this.onClick); }
    attributeChangedCallback() { this.syncState(); }
    onClick = (event) => {
        const trigger = event.target.closest("[data-inc-navbar-toggle],[data-inc-action='navbar-toggle']");
        if (!trigger || !this.contains(trigger)) return;
        event.preventDefault();
        this.toggleAttribute("open");
        this.syncState();
    };
    syncSlots() {
        moveSlots(this, {
            brand: "inc-navbar__brand",
            nav: "inc-navbar__nav",
            utilities: "inc-navbar__utilities",
            collapse: "inc-navbar__collapse",
            toggle: "inc-navbar__toggler",
        });
    }
    syncState() {
        this.classList.toggle("inc-navbar--app", this.hasAttribute("app"));
        this.setAttribute("aria-expanded", this.hasAttribute("open") ? "true" : "false");
        Array.from(this.classList).filter((token) => token.startsWith("inc-navbar--expand-")).forEach((token) => this.classList.remove(token));
        const bp = (this.getAttribute("breakpoint") || "").toLowerCase();
        if (bp) this.classList.add(`inc-navbar--expand-${bp}`);
    }
}

function findPanel(host, tab, index) {
    const idTarget = tab.getAttribute("aria-controls") || tab.getAttribute("data-inc-target");
    if (idTarget) {
        const id = idTarget.startsWith("#") ? idTarget.slice(1) : idTarget;
        const direct = host.querySelector(`#${id}`);
        if (direct instanceof HTMLElement) return direct;
    }

    return host._panels[index] || null;
}

class IncTabsElement extends IncElement {
    static get observedAttributes() { return ["selected", "variant"]; }
    connectedCallback() {
        this.ensureLayout();
        this.bindEvents();
        this.syncVariant();
        this.initTabs();
        this.syncSelected();
    }
    attributeChangedCallback(name) {
        if (!this.isConnected) return;
        if (name === "variant") {
            this.syncVariant();
            return;
        }
        if (this._reflectingSelected) return;
        this.syncSelected();
    }
    ensureLayout() {
        const nav = ensureNode(this, ".inc-tabs-nav", () => {
            const node = document.createElement("ul");
            node.className = "inc-tabs-nav";
            this.prepend(node);
            return node;
        });

        const content = ensureNode(this, ".inc-tab-content", () => {
            const node = document.createElement("div");
            node.className = "inc-tab-content";
            this.append(node);
            return node;
        });

        Array.from(this.children).filter((node) => node.getAttribute("slot") === "tab").forEach((tab) => {
            tab.removeAttribute("slot");
            addClass(tab, "inc-tab");
            const li = document.createElement("li");
            li.append(tab);
            nav.append(li);
        });

        Array.from(this.children).filter((node) => node.getAttribute("slot") === "panel").forEach((panel) => {
            panel.removeAttribute("slot");
            addClass(panel, "inc-tab-pane");
            content.append(panel);
        });

        this._tabs = Array.from(nav.querySelectorAll(".inc-tab, [role='tab']"));
        this._panels = Array.from(content.querySelectorAll(".inc-tab-pane, [role='tabpanel']"));
    }
    initTabs() {
        this._tabs.forEach((tab, index) => {
            tab.setAttribute("role", "tab");
            if (!tab.id) tab.id = `${this.id || this.localName}-tab-${index + 1}`;
            tab.tabIndex = index === 0 ? 0 : -1;
            const panel = findPanel(this, tab, index);
            if (!panel) return;
            panel.setAttribute("role", "tabpanel");
            panel.setAttribute("aria-labelledby", tab.id);
            if (!panel.id) panel.id = `${tab.id}-panel`;
            tab.setAttribute("aria-controls", panel.id);
        });

        const active = this._tabs.find((tab) => tab.classList.contains("active")) || this._tabs[0];
        if (active) this.activate(active, { emitEvents: false, focus: false });
    }
    bindEvents() {
        if (this._bound) return;
        this._bound = true;
        this.addEventListener("click", (event) => {
            const tab = event.target.closest(".inc-tab,[role='tab']");
            if (!tab || !this.contains(tab)) return;
            event.preventDefault();
            this.activate(tab, { emitEvents: true, focus: true });
        });
        this.addEventListener("keydown", (event) => {
            const tab = event.target.closest(".inc-tab,[role='tab']");
            if (!tab || !this.contains(tab)) return;
            const current = this._tabs.indexOf(tab);
            if (current < 0) return;
            if (event.key === "ArrowRight" || event.key === "ArrowDown") {
                event.preventDefault();
                this.activate(this._tabs[(current + 1) % this._tabs.length], { emitEvents: true, focus: true });
            } else if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
                event.preventDefault();
                this.activate(this._tabs[(current - 1 + this._tabs.length) % this._tabs.length], { emitEvents: true, focus: true });
            }
        });
    }
    activate(tab, options = {}) {
        if (!(tab instanceof HTMLElement)) return;
        const previous = this._tabs.find((item) => item.getAttribute("aria-selected") === "true") || null;
        const nextSelected = tab.id || "";
        this._tabs.forEach((item, index) => {
            const active = item === tab;
            item.classList.toggle("active", active);
            item.setAttribute("aria-selected", active ? "true" : "false");
            item.tabIndex = active ? 0 : -1;
            const panel = findPanel(this, item, index);
            if (panel) {
                panel.hidden = !active;
                panel.classList.toggle("active", active);
                panel.classList.toggle("show", active);
            }
        });
        if (this.getAttribute("selected") !== nextSelected) {
            this._reflectingSelected = true;
            try {
                this.setAttribute("selected", nextSelected);
            } finally {
                this._reflectingSelected = false;
            }
        }
        if (options.focus) tab.focus();
        if (options.emitEvents !== false) {
            this.emit("select", { previous: previous?.id || null, selected: tab.id, tab });
            this.emit("change", { previous: previous?.id || null, selected: tab.id, tab });
        }
    }
    syncSelected() {
        const selected = this.getAttribute("selected");
        if (!selected || !this._tabs?.length) return;
        const next = this._tabs.find((tab) => tab.id === selected) || null;
        const active = this._tabs.find((tab) => tab.getAttribute("aria-selected") === "true") || null;
        if (!next || next === active) return;
        this.activate(next, { emitEvents: false, focus: false });
    }
    syncVariant() {
        const variant = (this.getAttribute("variant") || "line").toLowerCase();
        this.classList.remove("inc-tabs-line", "inc-tabs-folder");
        this.classList.add(variant === "folder" ? "inc-tabs-folder" : "inc-tabs-line");
    }
}

function firstMenuItem(menuRoot) {
    return menuRoot?.querySelector("[role='menuitem'],button,a,[tabindex]:not([tabindex='-1'])") || null;
}

class IncUserMenuElement extends IncElement {
    static get observedAttributes() { return ["open", "variant", "label"]; }
    connectedCallback() {
        this.ensureLayout();
        this.bindEvents();
        this.syncState();
    }
    attributeChangedCallback() { this.syncState(); }
    open() { this.setAttribute("open", ""); }
    close() { this.removeAttribute("open"); }
    ensureLayout() {
        const details = ensureNode(this, "details.inc-native-menu", () => {
            const node = document.createElement("details");
            node.className = "inc-native-menu";
            node.innerHTML = `<summary class="inc-native-menu__summary"></summary><div class="inc-native-menu__panel" role="menu"></div>`;
            this.append(node);
            return node;
        });
        this._details = details;
        this._summary = details.querySelector(":scope > .inc-native-menu__summary");
        this._panel = details.querySelector(":scope > .inc-native-menu__panel");
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "trigger").forEach((node) => {
            node.removeAttribute("slot");
            this._summary.replaceChildren(node);
        });
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "menu").forEach((node) => {
            node.removeAttribute("slot");
            this._panel.replaceChildren(node);
        });
    }
    bindEvents() {
        if (this._bound) return;
        this._bound = true;
        this._details.addEventListener("toggle", () => {
            this.toggleAttribute("open", this._details.open);
            this.emit(this._details.open ? "open" : "close");
        });
        this.addEventListener("click", (event) => {
            const action = event.target.closest("[role='menuitem'],button,a");
            if (!action || !this._panel.contains(action)) return;
            this.emit("select", {
                value: action.getAttribute("value") || action.getAttribute("data-value") || action.textContent?.trim() || "",
                text: action.textContent?.trim() || "",
                item: action,
            });
            this.close();
            this._summary?.focus();
        });
        this.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && this._details.open) {
                event.preventDefault();
                this.close();
                this._summary?.focus();
                return;
            }
            const trigger = event.target.closest("summary,.inc-native-menu__summary,[slot='trigger'],button");
            if (!trigger || !this.contains(trigger)) return;
            if (event.key === "Enter" || event.key === " " || event.key === "ArrowDown") {
                event.preventDefault();
                this.open();
                requestAnimationFrame(() => firstMenuItem(this._panel)?.focus());
            }
        });
    }
    syncState() {
        this._details.classList.toggle("inc-native-menu--navbar", (this.getAttribute("variant") || "").toLowerCase() === "navbar");
        this._details.open = this.hasAttribute("open");
    }
}

class IncFieldElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-form__group");
        moveSlots(this, { label: "inc-form__label", hint: "inc-form__hint", error: "inc-form__feedback", control: "inc-form__control" });
    }
}

class IncInputGroupElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-input-group");
        moveSlots(this, { prefix: "inc-input-group__text", suffix: "inc-input-group__text", control: "inc-form__control" });
    }
}

class IncChoiceGroupElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-form__fieldset");
        let legend = byClass(this, "inc-form__legend");
        let choices = byClass(this, "inc-form__choices");
        if (!legend) {
            legend = document.createElement("legend");
            legend.className = "inc-form__legend";
            this.prepend(legend);
        }
        if (!choices) {
            choices = document.createElement("div");
            choices.className = "inc-form__choices";
            this.append(choices);
        }
        if (this.hasAttribute("legend")) legend.textContent = this.getAttribute("legend");
        if (this.hasAttribute("inline")) choices.classList.add("inc-form__choices--inline");
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "item").forEach((node) => {
            node.removeAttribute("slot");
            addClass(node, "inc-form__check");
            choices.append(node);
        });
    }
}

class IncReadonlyFieldElement extends IncElement {
    connectedCallback() { addClass(this, "inc-readonly-field"); }
}

class IncValidationSummaryElement extends IncElement {
    connectedCallback() {
        addClass(this, "inc-form__error-summary");
        let title = byClass(this, "inc-form__error-summary-title");
        let list = byClass(this, "inc-form__error-summary-list");
        if (!title) {
            title = document.createElement("h3");
            title.className = "inc-form__error-summary-title";
            this.prepend(title);
        }
        if (!list) {
            list = document.createElement("ul");
            list.className = "inc-form__error-summary-list";
            this.append(list);
        }
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "item").forEach((node) => {
            node.removeAttribute("slot");
            if (node.tagName === "LI") list.append(node);
            else {
                const li = document.createElement("li");
                li.append(node);
                list.append(li);
            }
        });
        if (this.hasAttribute("title")) title.textContent = this.getAttribute("title");
        else if (this.hasAttribute("count")) title.textContent = `There are ${this.getAttribute("count")} issues to fix`;
    }
}

class IncStatePanelElement extends IncElement {
    static get observedAttributes() { return ["variant", "tone", "open"]; }
    connectedCallback() {
        addClass(this, "inc-state-panel");
        moveSlots(this, { icon: "inc-state-panel__icon", title: "inc-state-panel__title", body: "inc-state-panel__body", actions: "inc-state-panel__actions" });
        this.syncState();
    }
    attributeChangedCallback() { this.syncState(); }
    syncState() {
        Array.from(this.classList).filter((token) => token.startsWith("inc-state-panel--")).forEach((token) => this.classList.remove(token));
        const variant = (this.getAttribute("variant") || this.getAttribute("tone") || "").toLowerCase();
        if (variant) this.classList.add(`inc-state-panel--${variant}`);
        const open = this.hasAttribute("open") ? toBoolean(this.getAttribute("open"), true) : true;
        this.hidden = !open;
        this.setAttribute("aria-hidden", open ? "false" : "true");
    }
}

class IncLiveRegionElement extends IncElement {
    static get observedAttributes() { return ["politeness", "atomic", "busy"]; }
    connectedCallback() {
        addClass(this, "inc-live-region");
        this.syncAria();
    }
    attributeChangedCallback() { this.syncAria(); }
    announce(message) {
        let node = this.querySelector(":scope > [data-inc-live-region-message]");
        if (!node) {
            node = document.createElement("span");
            node.dataset.incLiveRegionMessage = "true";
            node.className = "inc-u-visually-hidden";
            this.append(node);
        }
        node.textContent = "";
        requestAnimationFrame(() => { node.textContent = message == null ? "" : String(message); });
    }
    syncAria() {
        const mode = (this.getAttribute("politeness") || "polite").toLowerCase();
        this.setAttribute("role", mode === "assertive" ? "alert" : "status");
        this.setAttribute("aria-live", mode === "assertive" ? "assertive" : "polite");
        this.setAttribute("aria-atomic", toBoolean(this.getAttribute("atomic"), true) ? "true" : "false");
        this.setAttribute("aria-busy", this.hasAttribute("busy") && toBoolean(this.getAttribute("busy")) ? "true" : "false");
    }
}

class IncAutoRefreshElement extends IncElement {
    static get observedAttributes() { return ["paused"]; }
    connectedCallback() {
        addClass(this, "inc-auto-refresh");
        this.ensureStructure();
        this.bindEvents();
        this.syncPaused();
    }
    attributeChangedCallback() { this.syncPaused(); }
    ensureStructure() {
        this._countdown = ensureNode(this, ".inc-auto-refresh__countdown", () => {
            const node = document.createElement("span");
            node.className = "inc-auto-refresh__countdown";
            node.innerHTML = `<span class="inc-auto-refresh__label">${this.getAttribute("label") || "Refresh in"}</span><span class="inc-auto-refresh__value">0s</span>`;
            return node;
        });
        this._status = ensureNode(this, ".inc-auto-refresh__status", () => {
            const node = document.createElement("span");
            node.className = "inc-auto-refresh__status";
            node.innerHTML = `<span class="inc-auto-refresh__status-text">${this.getAttribute("loading-label") || "Refreshing"}</span>`;
            return node;
        });
        this._toggle = ensureNode(this, ".inc-auto-refresh__toggle", () => {
            const node = document.createElement("button");
            node.type = "button";
            node.className = "inc-btn inc-btn--secondary inc-btn--micro inc-auto-refresh__toggle";
            node.innerHTML = `<span class="inc-auto-refresh__toggle-text">Pause</span>`;
            return node;
        });
        this._toggleText = this._toggle.querySelector(".inc-auto-refresh__toggle-text");
    }
    bindEvents() {
        if (this._bound) return;
        this._bound = true;
        this.addEventListener("click", (event) => {
            const trigger = event.target.closest(".inc-auto-refresh__toggle,[data-inc-action='auto-refresh-toggle']");
            if (!trigger || !this.contains(trigger)) return;
            event.preventDefault();
            this.toggleAttribute("paused");
            this.syncPaused();
            this.emit(this.hasAttribute("paused") ? "pause" : "resume");
        });
    }
    syncPaused() {
        const paused = this.hasAttribute("paused");
        this.classList.toggle("is-paused", paused);
        this.classList.remove("is-loading");
        this.setAttribute("aria-busy", "false");
        this._status.hidden = true;
        this._countdown.hidden = false;
        this._toggle.setAttribute("aria-pressed", paused ? "true" : "false");
        this._toggle.setAttribute("aria-label", paused ? "Resume" : "Pause");
        this._toggleText.textContent = paused ? "Resume" : "Pause";
    }
}

const themeRuntime = {
    initialized: false,
    mode: "system",
    resolved: "light",
    key: "inc-theme-mode",
    switchers: new Set(),
};

function resolveTheme(mode) {
    if (mode !== "system") return mode;
    return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function persistTheme(mode) {
    try {
        if (mode === "system") window.localStorage.removeItem(themeRuntime.key);
        else window.localStorage.setItem(themeRuntime.key, mode);
    } catch {
        // ignore storage restrictions
    }
}

function applyTheme(mode, options = {}) {
    const next = THEME_MODES.includes(mode) ? mode : "system";
    const resolved = resolveTheme(next);
    themeRuntime.mode = next;
    themeRuntime.resolved = resolved;
    const root = document.documentElement;
    root.setAttribute("data-inc-theme-mode", next);
    root.setAttribute("data-bs-theme", resolved);
    root.style.colorScheme = resolved;
    if (options.persist !== false) persistTheme(next);
    themeRuntime.switchers.forEach((switcher) => switcher.syncFromTheme?.());
    emit(root, "inc-theme-change", { mode: next, resolved });
}

function initTheme() {
    if (themeRuntime.initialized) return;
    themeRuntime.initialized = true;
    let stored = null;
    try {
        stored = window.localStorage.getItem(themeRuntime.key);
    } catch {
        stored = null;
    }
    applyTheme(THEME_MODES.includes(stored) ? stored : (document.documentElement.getAttribute("data-inc-theme-mode") || "system"), { persist: false });
}

class IncThemeSwitcherElement extends IncElement {
    static get observedAttributes() { return ["mode", "variant", "label", "storage-key"]; }
    connectedCallback() {
        if (this.hasAttribute("storage-key")) themeRuntime.key = this.getAttribute("storage-key");
        initTheme();
        this.ensureLayout();
        this.bindEvents();
        themeRuntime.switchers.add(this);
        this.syncFromTheme();
    }
    disconnectedCallback() { themeRuntime.switchers.delete(this); }
    attributeChangedCallback(name) {
        if (name === "mode") {
            applyTheme(this.getAttribute("mode"));
            return;
        }
        if (name === "storage-key") themeRuntime.key = this.getAttribute("storage-key") || "inc-theme-mode";
        if (!this.isConnected) return;
        this.syncFromTheme();
    }
    getMode() { return themeRuntime.mode; }
    getResolvedTheme() { return themeRuntime.resolved; }
    setMode(mode) { applyTheme(mode); }
    cycleMode() {
        const index = THEME_MODES.indexOf(themeRuntime.mode);
        applyTheme(THEME_MODES[(index + 1) % THEME_MODES.length]);
    }
    ensureLayout() {
        const details = ensureNode(this, "details.inc-theme-switcher", () => {
            const node = document.createElement("details");
            node.className = "inc-native-menu inc-theme-switcher";
            node.innerHTML = `<summary class="inc-native-menu__summary inc-theme-switcher__summary"><span class="inc-theme-switcher__meta"><span class="inc-theme-switcher__label">Theme</span><span class="inc-theme-switcher__status"></span></span></summary><div class="inc-native-menu__panel inc-theme-switcher__panel" role="menu"></div>`;
            this.append(node);
            return node;
        });
        this._details = details;
        this._summary = details.querySelector(":scope > .inc-theme-switcher__summary");
        this._status = details.querySelector(":scope .inc-theme-switcher__status");
        this._label = details.querySelector(":scope .inc-theme-switcher__label");
        this._panel = details.querySelector(":scope > .inc-theme-switcher__panel");
        if (!this._panel.querySelector(".inc-theme-switcher__option")) {
            THEME_MODES.forEach((mode) => {
                const option = document.createElement("button");
                option.type = "button";
                option.className = "inc-theme-switcher__option";
                option.setAttribute("role", "menuitemradio");
                option.setAttribute("data-inc-theme-mode", mode);
                option.innerHTML = `<span class="inc-theme-switcher__option-body"><span class="inc-theme-switcher__option-label">${mode[0].toUpperCase()}${mode.slice(1)}</span></span>`;
                this._panel.append(option);
            });
        }
    }
    bindEvents() {
        if (this._bound) return;
        this._bound = true;
        this.addEventListener("click", (event) => {
            const option = event.target.closest("[data-inc-theme-mode]");
            if (!option || !this.contains(option)) return;
            event.preventDefault();
            applyTheme(option.getAttribute("data-inc-theme-mode"));
            this._details.open = false;
            this._summary.focus();
        });
        this.addEventListener("keydown", (event) => {
            if (event.key === "Escape" && this._details.open) {
                event.preventDefault();
                this._details.open = false;
                this._summary.focus();
            }
        });
    }
    syncFromTheme() {
        if (!this._details) return;
        this._details.classList.toggle("inc-native-menu--navbar", (this.getAttribute("variant") || "").toLowerCase() === "navbar");
        this._label.textContent = this.getAttribute("label") || "Theme";
        this._status.textContent = themeRuntime.mode === "system"
            ? `System (${themeRuntime.resolved[0].toUpperCase()}${themeRuntime.resolved.slice(1)})`
            : `${themeRuntime.mode[0].toUpperCase()}${themeRuntime.mode.slice(1)}`;
        this.querySelectorAll("[data-inc-theme-mode]").forEach((option) => {
            const selected = option.getAttribute("data-inc-theme-mode") === themeRuntime.mode;
            option.classList.toggle("is-selected", selected);
            option.setAttribute("aria-checked", selected ? "true" : "false");
        });
    }
}

class IncDisclosureElement extends IncElement {
    static get observedAttributes() { return ["open", "summary"]; }
    connectedCallback() {
        const details = ensureNode(this, "details.inc-disclosure", () => {
            const node = document.createElement("details");
            node.className = "inc-disclosure";
            node.innerHTML = `<summary class="inc-disclosure__summary"></summary><div class="inc-disclosure__content"></div>`;
            this.append(node);
            return node;
        });
        this._details = details;
        this._summary = details.querySelector(":scope > .inc-disclosure__summary");
        this._content = details.querySelector(":scope > .inc-disclosure__content");
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "summary").forEach((node) => {
            node.removeAttribute("slot");
            this._summary.replaceChildren(node);
        });
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "content").forEach((node) => {
            node.removeAttribute("slot");
            this._content.append(node);
        });
        this._details.addEventListener("toggle", () => {
            this.toggleAttribute("open", this._details.open);
            this.emit(this._details.open ? "open" : "close", { open: this._details.open });
            this.emit("toggle", { open: this._details.open });
        });
        this.syncState();
    }
    attributeChangedCallback() { this.syncState(); }
    syncState() {
        if (this._summary && this.hasAttribute("summary") && !this._summary.textContent?.trim()) {
            this._summary.textContent = this.getAttribute("summary");
        }
        if (this._details) this._details.open = this.hasAttribute("open");
    }
}

class IncDialogBaseElement extends IncElement {
    static get observedAttributes() { return ["open", "modal", "dismissible", "label"]; }
    connectedCallback() {
        this.ensureLayout(this.drawerMode === true);
        this.bindEvents();
        this.syncState();
    }
    attributeChangedCallback() { this.syncState(); }
    show() { this.openDialog(false); }
    showModal() { this.openDialog(true); }
    close(returnValue = "") {
        if (this._dialog?.open) this._dialog.close(returnValue);
        this.removeAttribute("open");
    }
    ensureLayout(drawerMode) {
        const dialog = ensureNode(this, "dialog.inc-native-dialog", () => {
            const node = document.createElement("dialog");
            node.className = "inc-native-dialog";
            node.innerHTML = `<div class="inc-native-dialog__surface"><div class="inc-native-dialog__header"><div class="inc-native-dialog__titles"><h2 class="inc-native-dialog__title">${this.getAttribute("label") || "Dialog"}</h2></div><button type="button" class="inc-native-dialog__close" data-inc-dialog-close aria-label="Close dialog">×</button></div><div class="inc-native-dialog__body"></div><div class="inc-native-dialog__footer"></div></div>`;
            this.append(node);
            return node;
        });
        dialog.classList.toggle("inc-native-dialog--drawer", drawerMode);
        this._dialog = dialog;
        this._header = dialog.querySelector(".inc-native-dialog__header");
        this._titles = dialog.querySelector(".inc-native-dialog__titles");
        this._body = dialog.querySelector(".inc-native-dialog__body");
        this._footer = dialog.querySelector(".inc-native-dialog__footer");
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "header" || node.getAttribute("slot") === "title").forEach((node) => {
            node.removeAttribute("slot");
            (this._titles || this._header).append(node);
        });
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "body").forEach((node) => {
            node.removeAttribute("slot");
            this._body.append(node);
        });
        Array.from(this.children).filter((node) => node.getAttribute("slot") === "footer").forEach((node) => {
            node.removeAttribute("slot");
            this._footer.append(node);
        });
    }
    bindEvents() {
        if (this._bound) return;
        this._bound = true;
        this.addEventListener("click", (event) => {
            const closeButton = event.target.closest("[data-inc-dialog-close]");
            if (!closeButton || !this.contains(closeButton)) return;
            event.preventDefault();
            this.close();
        });
        this._dialog.addEventListener("close", () => {
            this.removeAttribute("open");
            this.emit("close", { returnValue: this._dialog.returnValue || "" });
            if (this._returnFocus instanceof HTMLElement) this._returnFocus.focus();
        });
        this._dialog.addEventListener("cancel", (event) => {
            this.emit("cancel");
            if (!toBoolean(this.getAttribute("dismissible"), true)) event.preventDefault();
        });
    }
    openDialog(forceModal) {
        if (!(this._dialog instanceof HTMLDialogElement) || this._dialog.open) return;
        this._returnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        const modal = forceModal || toBoolean(this.getAttribute("modal"), true);
        if (modal && typeof this._dialog.showModal === "function") this._dialog.showModal();
        else if (typeof this._dialog.show === "function") this._dialog.show();
        const initialFocus = this._dialog.querySelector("[data-inc-initial-focus]");
        if (initialFocus instanceof HTMLElement) initialFocus.focus();
        this.setAttribute("open", "");
        this.emit("open");
    }
    syncState() {
        if (!(this._dialog instanceof HTMLDialogElement)) return;
        const wantsOpen = this.hasAttribute("open");
        if (wantsOpen && !this._dialog.open) this.openDialog(toBoolean(this.getAttribute("modal"), true));
        else if (!wantsOpen && this._dialog.open) this._dialog.close();
    }
}

class IncDialogElement extends IncDialogBaseElement {
    get drawerMode() { return false; }
}

class IncDrawerElement extends IncDialogBaseElement {
    get drawerMode() { return true; }
}

const ENTRIES = [
    ["inc-app-shell", IncAppShellElement],
    ["inc-page", IncPageElement],
    ["inc-page-header", IncPageHeaderElement],
    ["inc-section", IncSectionElement],
    ["inc-card", IncCardElement],
    ["inc-summary-overview", IncSummaryOverviewElement],
    ["inc-summary-block", IncSummaryBlockElement],
    ["inc-footer-bar", IncFooterBarElement],
    ["inc-navbar", IncNavbarElement],
    ["inc-tabs", IncTabsElement],
    ["inc-user-menu", IncUserMenuElement],
    ["inc-field", IncFieldElement],
    ["inc-input-group", IncInputGroupElement],
    ["inc-choice-group", IncChoiceGroupElement],
    ["inc-readonly-field", IncReadonlyFieldElement],
    ["inc-validation-summary", IncValidationSummaryElement],
    ["inc-state-panel", IncStatePanelElement],
    ["inc-live-region", IncLiveRegionElement],
    ["inc-auto-refresh", IncAutoRefreshElement],
    ["inc-theme-switcher", IncThemeSwitcherElement],
    ["inc-disclosure", IncDisclosureElement],
    ["inc-dialog", IncDialogElement],
    ["inc-drawer", IncDrawerElement],
];

function defineAll(options = {}) {
    const registry = options.registry || globalThis.customElements;
    if (!registry || typeof registry.define !== "function" || typeof registry.get !== "function") return [];

    return ENTRIES.map(([name, ctor]) => {
        const existing = registry.get(name);
        if (existing) {
            return {
                name,
                defined: existing === ctor,
                reason: existing === ctor ? "already-defined" : "name-conflict",
                constructor: existing,
            };
        }

        registry.define(name, ctor);
        return { name, defined: true, reason: "defined", constructor: ctor };
    });
}

function registerIncWebComponents(options = {}) {
    return defineAll(options);
}

if (typeof globalThis !== "undefined") {
    globalThis.IncWebComponents = globalThis.IncWebComponents || {};
    globalThis.IncWebComponents.defineAll = defineAll;
    globalThis.IncWebComponents.registerIncWebComponents = registerIncWebComponents;
    globalThis.IncWebComponents.components = new Map(ENTRIES);
}

defineAll();

export {
    defineAll,
    registerIncWebComponents,
};
