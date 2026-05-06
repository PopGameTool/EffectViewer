import { dotnet } from './_framework/dotnet.js'
import { initializeProjectStorage, persistCurrentStorage } from './browserProjectStorage.js'
import { installMobileImeWorkaround } from './mobileImeWorkaround.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

configureMobileViewport();
installMobileImeWorkaround();

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

await initializeProjectStorage(dotnetRuntime.Module, "/EffectViewer");

window.addEventListener("pagehide", () => {
    persistCurrentStorage().catch(error => {
        console.warn("Could not persist EffectViewer project storage during pagehide.", error);
    });
});

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);

function configureMobileViewport() {
    const root = document.documentElement;
    const viewport = window.visualViewport;

    let pendingFrame = 0;
    const applyViewportSize = () => {
        pendingFrame = 0;

        const width = viewport?.width ?? window.innerWidth;
        const height = viewport?.height ?? window.innerHeight;
        const offsetLeft = viewport?.offsetLeft ?? 0;
        const offsetTop = viewport?.offsetTop ?? 0;

        root.style.setProperty("--effect-viewer-viewport-width", `${width}px`);
        root.style.setProperty("--effect-viewer-viewport-height", `${height}px`);
        root.style.setProperty("--effect-viewer-viewport-left", `${offsetLeft}px`);
        root.style.setProperty("--effect-viewer-viewport-top", `${offsetTop}px`);
    };

    const scheduleViewportSize = () => {
        if (pendingFrame) {
            return;
        }

        pendingFrame = window.requestAnimationFrame(applyViewportSize);
    };

    const scheduleSettledViewportSize = () => {
        scheduleViewportSize();
        window.setTimeout(scheduleViewportSize, 80);
        window.setTimeout(scheduleViewportSize, 260);
    };

    applyViewportSize();
    window.addEventListener("resize", scheduleSettledViewportSize, { passive: true });
    window.addEventListener("orientationchange", scheduleSettledViewportSize, { passive: true });
    document.addEventListener("focusin", scheduleSettledViewportSize, true);
    document.addEventListener("focusout", scheduleSettledViewportSize, true);

    if (viewport) {
        viewport.addEventListener("resize", scheduleSettledViewportSize, { passive: true });
        viewport.addEventListener("scroll", scheduleSettledViewportSize, { passive: true });
    }
}
