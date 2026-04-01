(() => {
    const editorSelector = "[data-array-editor]";
    const sourceSelector = "[data-array-input]";
    const listSelector = "[data-array-list]";
    const addSelector = '[data-array-action="add"]';
    const itemSelector = "[data-array-item-input]";

    const toLines = (value) => String(value || "")
        .replace(/\r\n/g, "\n")
        .split("\n")
        .map((line) => line.trim())
        .filter((line) => line.length > 0);

    const syncSource = (editor) => {
        const source = editor.querySelector(sourceSelector);
        if (!(source instanceof HTMLInputElement || source instanceof HTMLTextAreaElement)) {
            return;
        }

        const values = Array.from(editor.querySelectorAll(itemSelector))
            .map((input) => input.value.trim())
            .filter((line) => line.length > 0);

        source.value = values.join("\n");
        source.dispatchEvent(new Event("input", { bubbles: true }));
        source.dispatchEvent(new Event("change", { bubbles: true }));
    };

    const updateButtons = (editor) => {
        const rows = Array.from(editor.querySelectorAll("[data-array-row]"));
        for (const row of rows) {
            const upButton = row.querySelector('[data-array-action="up"]');
            const downButton = row.querySelector('[data-array-action="down"]');
            if (upButton instanceof HTMLButtonElement) {
                upButton.disabled = row === rows[0];
            }
            if (downButton instanceof HTMLButtonElement) {
                downButton.disabled = row === rows[rows.length - 1];
            }
        }
    };

    const createRow = (editor, value = "") => {
        const row = document.createElement("div");
        row.className = "array-editor__row";
        row.setAttribute("data-array-row", "true");

        const input = document.createElement("input");
        input.type = "text";
        input.className = "inc-form__control array-editor__input";
        input.setAttribute("data-array-item-input", "true");
        input.value = value;
        input.placeholder = editor.dataset.arrayPlaceholder || "Add item";
        input.autocomplete = "off";
        input.spellcheck = false;

        const actions = document.createElement("div");
        actions.className = "array-editor__actions";

        const makeButton = (label, action) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "inc-btn inc-btn--secondary inc-btn--sm array-editor__button";
            button.textContent = label;
            button.setAttribute("data-array-action", action);
            return button;
        };

        actions.append(
            makeButton("Up", "up"),
            makeButton("Down", "down"),
            makeButton("Remove", "remove"),
        );

        input.addEventListener("input", () => syncSource(editor));
        input.addEventListener("keydown", (event) => {
            if (event.key !== "Enter") {
                return;
            }

            event.preventDefault();
            const nextRow = createRow(editor, "");
            row.after(nextRow);
            syncSource(editor);
            updateButtons(editor);
            const nextInput = nextRow.querySelector(itemSelector);
            if (nextInput instanceof HTMLInputElement) {
                nextInput.focus();
            }
        });

        row.append(input, actions);
        return row;
    };

    const moveRow = (row, direction) => {
        if (direction === "up" && row.previousElementSibling) {
            row.previousElementSibling.before(row);
        } else if (direction === "down" && row.nextElementSibling) {
            row.nextElementSibling.after(row);
        }
    };

    const enhanceEditor = (editor) => {
        if (editor.dataset.arrayEnhanced === "true") {
            return;
        }

        const source = editor.querySelector(sourceSelector);
        const list = editor.querySelector(listSelector);
        const addButton = editor.querySelector(addSelector);
        if (!(source instanceof HTMLInputElement || source instanceof HTMLTextAreaElement) ||
            !(list instanceof HTMLElement) ||
            !(addButton instanceof HTMLButtonElement)) {
            return;
        }

        editor.dataset.arrayEnhanced = "true";
        source.hidden = true;
        source.setAttribute("aria-hidden", "true");

        const values = toLines(source.value);
        list.replaceChildren();
        if (values.length === 0) {
            list.append(createRow(editor, ""));
        } else {
            for (const value of values) {
                list.append(createRow(editor, value));
            }
        }

        list.addEventListener("click", (event) => {
            const button = event.target instanceof HTMLElement
                ? event.target.closest("[data-array-action]")
                : null;
            if (!(button instanceof HTMLButtonElement)) {
                return;
            }

            const action = button.getAttribute("data-array-action");
            const row = button.closest("[data-array-row]");
            if (!(row instanceof HTMLElement) || !action) {
                return;
            }

            if (action === "remove") {
                const remainingRows = list.querySelectorAll("[data-array-row]").length;
                row.remove();
                if (remainingRows === 1) {
                    list.append(createRow(editor, ""));
                }
            } else if (action === "up" || action === "down") {
                moveRow(row, action);
            }

            syncSource(editor);
            updateButtons(editor);
        });

        addButton.addEventListener("click", () => {
            const row = createRow(editor, "");
            list.append(row);
            syncSource(editor);
            updateButtons(editor);
            const input = row.querySelector(itemSelector);
            if (input instanceof HTMLInputElement) {
                input.focus();
            }
        });

        syncSource(editor);
        updateButtons(editor);
    };

    const init = () => {
        document.querySelectorAll(editorSelector).forEach(enhanceEditor);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init, { once: true });
    } else {
        init();
    }
})();
