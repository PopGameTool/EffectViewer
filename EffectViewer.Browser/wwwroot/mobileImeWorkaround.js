export function installMobileImeWorkaround() {
    if (!isMobileLikeBrowser()) {
        return;
    }

    const root = document.getElementById("out");
    if (!root || root.__effectViewerMobileImeWorkaroundInstalled) {
        return;
    }

    root.__effectViewerMobileImeWorkaroundInstalled = true;

    const originalPrepend = root.prepend;
    root.prepend = function (...nodes) {
        originalPrepend.apply(this, nodes);

        for (const node of nodes) {
            if (node instanceof HTMLInputElement && node.classList.contains("avalonia-input-element")) {
                configureInputElement(node);
                window.requestAnimationFrame(() => attachInputElementHandlers(node));
            }
        }
    };
}

function attachInputElementHandlers(inputElement) {
    if (inputElement.__effectViewerMobileImeHandlersInstalled) {
        return;
    }

    const parent = inputElement.parentElement;
    if (!parent) {
        return;
    }

    inputElement.__effectViewerMobileImeHandlersInstalled = true;

    const state = {
        beforeInputSnapshot: snapshotInput(inputElement),
        lastTransferKey: undefined,
        transferInputToIme: false,
        withinComposition: false,
        waitingForCompositionInput: false
    };

    inputElement.addEventListener("beforeinput", () => {
        state.beforeInputSnapshot = snapshotInput(inputElement);
    }, true);

    parent.addEventListener("keydown", event => {
        if (event.code === "" && (event.key === "Unidentified" || event.key === "Process")) {
            state.transferInputToIme = true;
            state.lastTransferKey = event.key;
        } else {
            state.transferInputToIme = false;
            state.lastTransferKey = undefined;
        }
    });

    parent.addEventListener("compositionstart", () => {
        state.withinComposition = true;
    });

    parent.addEventListener("compositionend", () => {
        state.withinComposition = false;
    });

    parent.addEventListener("beforeinput", () => {
        state.waitingForCompositionInput = state.withinComposition;
    });

    inputElement.addEventListener("input", event => {
        if (!(event instanceof InputEvent)) {
            return;
        }

        if (state.waitingForCompositionInput) {
            state.waitingForCompositionInput = false;
            return;
        }

        if (state.withinComposition) {
            return;
        }

        if (event.inputType === "deleteContentBackward") {
            dispatchKeyboardEvent(parent, "keydown", "Backspace", "Backspace");
            dispatchKeyboardEvent(parent, "keyup", "Backspace", "Backspace");
            return;
        }

        if (event.inputType === "insertLineBreak") {
            dispatchKeyboardEvent(parent, "keydown", "Enter", "Enter");
            dispatchKeyboardEvent(parent, "keyup", "Enter", "Enter");
            return;
        }

        if (!state.transferInputToIme &&
            event.inputType !== "insertText" &&
            event.inputType !== "insertCompositionText") {
            return;
        }

        const text = getInsertedText(state.beforeInputSnapshot.value, inputElement.value, event);
        if (!text) {
            return;
        }

        dispatchTextAsImeEvents(parent, inputElement, text, state);
    });
}

function configureInputElement(inputElement) {
    inputElement.style.caretColor = "transparent";
    inputElement.style.fontSize = "16px";
    inputElement.style.pointerEvents = "none";
    inputElement.autocomplete = "off";
    inputElement.autocorrect = "off";
    inputElement.autocapitalize = "none";
    inputElement.spellcheck = false;
}

function dispatchTextAsImeEvents(parent, inputElement, text, state) {
    const currentSnapshot = snapshotInput(inputElement);
    const beforeInputSnapshot = state.beforeInputSnapshot;

    syncSelectionToAvalonia(parent, inputElement, beforeInputSnapshot);

    parent.dispatchEvent(new CompositionEvent("compositionstart", {
        bubbles: true,
        data: ""
    }));

    parent.dispatchEvent(new CompositionEvent("compositionupdate", {
        bubbles: true,
        data: text
    }));

    dispatchKeyboardEvent(parent, "keydown", "", state.lastTransferKey ?? "Unidentified");

    parent.dispatchEvent(new InputEvent("beforeinput", {
        bubbles: true,
        data: text,
        inputType: "insertCompositionText",
        isComposing: true
    }));

    parent.dispatchEvent(new InputEvent("input", {
        bubbles: true,
        data: text,
        inputType: "insertCompositionText",
        isComposing: true
    }));

    parent.dispatchEvent(new CompositionEvent("compositionend", {
        bubbles: true,
        data: text
    }));

    restoreSelection(inputElement, currentSnapshot);
    state.beforeInputSnapshot = snapshotInput(inputElement);
}

function syncSelectionToAvalonia(parent, inputElement, snapshot) {
    restoreSelection(inputElement, snapshot);
    parent.dispatchEvent(new InputEvent("beforeinput", {
        bubbles: true,
        inputType: "deleteContentBackward"
    }));
}

function dispatchKeyboardEvent(parent, eventName, code, key) {
    parent.dispatchEvent(new KeyboardEvent(eventName, {
        bubbles: true,
        code,
        key
    }));
}

function getInsertedText(previousText, currentText, event) {
    if (typeof event.data === "string" && event.data.length > 0) {
        return event.data;
    }

    let prefixLength = 0;
    while (
        prefixLength < previousText.length &&
        prefixLength < currentText.length &&
        previousText[prefixLength] === currentText[prefixLength]) {
        prefixLength++;
    }

    let suffixLength = 0;
    while (
        suffixLength < previousText.length - prefixLength &&
        suffixLength < currentText.length - prefixLength &&
        previousText[previousText.length - suffixLength - 1] === currentText[currentText.length - suffixLength - 1]) {
        suffixLength++;
    }

    return currentText.slice(prefixLength, currentText.length - suffixLength);
}

function snapshotInput(inputElement) {
    const value = inputElement.value ?? "";
    return {
        value,
        selectionStart: inputElement.selectionStart ?? value.length,
        selectionEnd: inputElement.selectionEnd ?? value.length
    };
}

function restoreSelection(inputElement, snapshot) {
    inputElement.setSelectionRange(
        clamp(snapshot.selectionStart, 0, inputElement.value.length),
        clamp(snapshot.selectionEnd, 0, inputElement.value.length));
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function isMobileLikeBrowser() {
    const userAgent = navigator.userAgent ?? "";
    return navigator.maxTouchPoints > 0 ||
        /Android|iPhone|iPad|iPod|Mobile/i.test(userAgent);
}
