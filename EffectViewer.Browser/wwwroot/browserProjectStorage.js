const databaseName = "EffectViewer.ProjectStorage";
const databaseVersion = 1;
const fileStoreName = "files";
const defaultRootPath = "/EffectViewer";

let activeModule = null;
let activeRootPath = defaultRootPath;
let persistInFlight = null;
let persistRequested = false;

export async function initializeProjectStorage(module, rootPath = defaultRootPath) {
    if (!module?.FS) {
        throw new Error("The .NET runtime file system is not available.");
    }

    activeModule = module;
    activeRootPath = normalizeRootPath(rootPath);
    ensureDirectory(activeModule.FS, activeRootPath);

    const db = await openDatabase();
    try {
        const records = await readAllFiles(db);
        for (const record of records) {
            if (!isPathInRoot(record.path, activeRootPath)) {
                continue;
            }

            const data = record.data instanceof Uint8Array
                ? record.data
                : new Uint8Array(record.data ?? []);
            ensureDirectory(activeModule.FS, dirname(record.path));
            activeModule.FS.writeFile(record.path, data);
        }
    } finally {
        db.close();
    }
}

export function persistCurrentStorage() {
    if (!activeModule?.FS) {
        return Promise.resolve();
    }

    persistRequested = true;
    if (!persistInFlight) {
        persistInFlight = runPersistQueue().finally(() => {
            persistInFlight = null;
        });
    }

    return persistInFlight;
}

async function runPersistQueue() {
    while (persistRequested) {
        persistRequested = false;
        await persistSnapshot();
    }
}

async function persistSnapshot() {
    const fs = activeModule.FS;
    ensureDirectory(fs, activeRootPath);
    const files = collectFiles(fs, activeRootPath);
    const db = await openDatabase();
    try {
        await writeFiles(db, files);
    } finally {
        db.close();
    }
}

function collectFiles(fs, rootPath) {
    const files = [];
    walkDirectory(fs, rootPath, files);
    return files;
}

function walkDirectory(fs, path, files) {
    for (const entry of fs.readdir(path)) {
        if (entry === "." || entry === "..") {
            continue;
        }

        const childPath = joinPath(path, entry);
        const stat = fs.stat(childPath);
        if (fs.isDir(stat.mode)) {
            walkDirectory(fs, childPath, files);
        } else if (fs.isFile(stat.mode)) {
            const data = fs.readFile(childPath);
            files.push({
                path: childPath,
                data: new Uint8Array(data)
            });
        }
    }
}

function openDatabase() {
    if (!globalThis.indexedDB) {
        return Promise.reject(new Error("IndexedDB is not available in this browser."));
    }

    return new Promise((resolve, reject) => {
        const request = indexedDB.open(databaseName, databaseVersion);
        request.onupgradeneeded = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains(fileStoreName)) {
                db.createObjectStore(fileStoreName, { keyPath: "path" });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error ?? new Error("Could not open browser project storage."));
        request.onblocked = () => reject(new Error("Browser project storage is blocked by another open tab."));
    });
}

function readAllFiles(db) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(fileStoreName, "readonly");
        const store = transaction.objectStore(fileStoreName);
        const request = store.getAll();
        request.onsuccess = () => resolve(request.result ?? []);
        request.onerror = () => reject(request.error ?? new Error("Could not read browser project storage."));
        transaction.onerror = () => reject(transaction.error ?? new Error("Could not read browser project storage."));
    });
}

function writeFiles(db, files) {
    return new Promise((resolve, reject) => {
        const transaction = db.transaction(fileStoreName, "readwrite");
        const store = transaction.objectStore(fileStoreName);
        const paths = new Set(files.map(file => file.path));

        const keysRequest = store.getAllKeys();
        keysRequest.onsuccess = () => {
            for (const key of keysRequest.result ?? []) {
                if (typeof key === "string" && isPathInRoot(key, activeRootPath) && !paths.has(key)) {
                    store.delete(key);
                }
            }

            for (const file of files) {
                store.put(file);
            }
        };
        keysRequest.onerror = () => reject(keysRequest.error ?? new Error("Could not update browser project storage."));
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error ?? new Error("Could not update browser project storage."));
    });
}

function ensureDirectory(fs, path) {
    if (!path || path === "/") {
        return;
    }

    const parts = path.split("/").filter(Boolean);
    let current = "";
    for (const part of parts) {
        current += `/${part}`;
        try {
            const stat = fs.stat(current);
            if (!fs.isDir(stat.mode)) {
                throw new Error(`${current} exists but is not a directory.`);
            }
        } catch {
            fs.mkdir(current);
        }
    }
}

function normalizeRootPath(path) {
    const normalized = (`/${path ?? defaultRootPath}`)
        .replace(/\\/g, "/")
        .replace(/\/+/g, "/");
    return normalized.length > 1
        ? normalized.replace(/\/$/, "")
        : defaultRootPath;
}

function isPathInRoot(path, rootPath) {
    return path === rootPath || path.startsWith(`${rootPath}/`);
}

function joinPath(parent, child) {
    return `${parent.replace(/\/$/, "")}/${child}`;
}

function dirname(path) {
    const index = path.lastIndexOf("/");
    return index <= 0 ? "/" : path.slice(0, index);
}
