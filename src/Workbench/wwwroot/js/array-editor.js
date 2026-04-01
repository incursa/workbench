(() => {
    const root = document.documentElement;
    const selector = "[data-array-editor]";

    const splitLines = (value) =>
        String(value ?? "")
            .replace(/\r\n/g, "\n")
            .split("\n")
            .map((line) => line.trim())
            .filter((line) => line.length > 0);

    const initEditor = (editor) => {
        const source = editor.querySelector("[data-array-editor-source]");
        const list = editor.querySelector("[data-array-editor-list]");
        const addButton = editor.querySelector("[data-array-editor-add]");

        if (!(source instanceof HTMLTextAreaElement || source instanceof HTMLInputElement) ||
            !(list instanceof HTMLElement) ||
            !(addButton instanceof HTMLButtonElement)) {
            return;
        }

        const syncSource = () => {
            const values = Array.from(list.querySelectorAll("[data-array-editor-input]"))
                .map((input) => input.value.trim())
                .filter((value) => value.length > 0);

            source.value = values.join("\n");
        };

        const updateButtonStates = () => {
            const rows = Array.from(list.children);
            rows.forEach((row, index) => {
                const buttons = row.querySelectorAll("button");
                if (buttons.length >= 3) {
                    buttons[0].disabled = index === 0;
                    buttons[1].disabled = index === rows.length - 1;
                }
            });
        };

        const createRow = (value = "") => {
            const row = document.createElement("div");
            row.className = "array-editor__row";

            const input = document.createElement("input");
            input.type = "text";
            input.className = "inc-form__control array-editor__input";
            input.dataset.arrayEditorInput = "true";
            input.value = value;
            input.placeholder = `Item ${list.children.length + 1}`;
            input.setAttribute("aria-label", `Item ${list.children.length + 1}`);
            input.addEventListener("input", syncSource);

            const actions = document.createElement("div");
            actions.className = "array-editor__actions";

            const moveUp = document.createElement("button");
            moveUp.type = "button";
            moveUp.className = "inc-btn inc-btn--secondary inc-btn--sm array-editor__button";
            moveUp.textContent = "Up";
            moveUp.addEventListener("click", () => {
                const previous = row.previousElementSibling;
                if (!previous) {
                    return;
                }

                row.parentElement?.insertBefore(row, previous);
                updateButtonStates();
                syncSource();
                input.focus();
            });

            const moveDown = document.createElement("button");
            moveDown.type = "button";
            moveDown.className = "inc-btn inc-btn--secondary inc-btn--sm array-editor__button";
            moveDown.textContent = "Down";
            moveDown.addEventListener("click", () => {
                const next = row.nextElementSibling;
                if (!next) {
                    return;
                }

                row.parentElement?.insertBefore(next, row);
                updateButtonStates();
                syncSource();
                input.focus();
            });

            const remove = document.createElement("button");
            remove.type = "button";
            remove.className = "inc-btn inc-btn--secondary inc-btn--sm array-editor__button";
            remove.textContent = "Remove";
            remove.addEventListener("click", () => {
                row.remove();
                if (list.children.length === 0) {
                    list.append(createRow());
                }

                updateButtonStates();
                syncSource();
            });

            actions.append(moveUp, moveDown, remove);
            row.append(input, actions);
            return row;
        };

        const renderFromSource = () => {
            const values = splitLines(source.value);
            list.replaceChildren();

            if (values.length === 0) {
                list.append(createRow());
            } else {
                values.forEach((value) => list.append(createRow(value)));
            }

            updateButtonStates();
            syncSource();
            root.dataset.arrayEditorReady = "true";
        };

        addButton.addEventListener("click", () => {
            list.append(createRow());
            updateButtonStates();
            syncSource();

            const lastInput = list.querySelector(":scope > .array-editor__row:last-child [data-array-editor-input]");
            if (lastInput instanceof HTMLInputElement) {
                lastInput.focus();
            }
        });

        source.addEventListener("input", renderFromSource);

        renderFromSource();
    };

    document.querySelectorAll(selector).forEach(initEditor);
})();
