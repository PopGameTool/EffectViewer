const FLOATS_PER_VERTEX = 8;
const BYTES_PER_FLOAT = 4;

export function createViewport() {
    const canvas = document.createElement("canvas");
    canvas.className = "effect-webgl-viewport";
    canvas.style.display = "block";
    canvas.style.width = "100%";
    canvas.style.height = "100%";
    canvas.style.background = "transparent";
    canvas.style.pointerEvents = "none";
    canvas.style.touchAction = "none";

    const gl = canvas.getContext("webgl2", contextOptions()) ||
        canvas.getContext("webgl", contextOptions()) ||
        canvas.getContext("experimental-webgl", contextOptions());

    if (!gl) {
        throw new Error("WebGL is not available for EffectViewer preview rendering.");
    }

    canvas.__effectViewerWebGl = createState(gl);
    canvas.addEventListener("webglcontextlost", event => event.preventDefault(), false);
    return canvas;
}

export function destroyViewport(canvas) {
    const state = getState(canvas);
    const gl = state.gl;
    clearTextures(canvas);

    if (state.vertexBuffer) {
        gl.deleteBuffer(state.vertexBuffer);
    }

    if (state.program) {
        gl.deleteProgram(state.program);
    }

    delete canvas.__effectViewerWebGl;
}

export function clearTextures(canvas) {
    const state = getState(canvas);
    const gl = state.gl;
    for (const texture of state.textures.values()) {
        gl.deleteTexture(texture);
    }

    state.textures.clear();
}

export function getMaxTextureSize(canvas) {
    const state = getState(canvas);
    const value = state.gl.getParameter(state.gl.MAX_TEXTURE_SIZE) | 0;
    return value > 0 ? value : 4096;
}

export function setDialogOverlayActive(active) {
    document.body.classList.toggle("effect-viewer-dialog-open", !!active);
}

export function uploadTexture(canvas, id, width, height, rgbaPixels, byteCount) {
    const state = getState(canvas);
    const gl = state.gl;
    const pixels = toTypedArray(rgbaPixels, byteCount, Uint8Array);
    if (width <= 0 || height <= 0 || pixels.byteLength < width * height * 4) {
        return false;
    }

    let texture = state.textures.get(id);
    if (!texture) {
        texture = gl.createTexture();
        if (!texture) {
            return false;
        }

        state.textures.set(id, texture);
    }

    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    const uploadPixels = createPremultipliedPixels(pixels.subarray(0, width * height * 4));
    clearGlErrors(gl);
    gl.texImage2D(
        gl.TEXTURE_2D,
        0,
        gl.RGBA,
        width,
        height,
        0,
        gl.RGBA,
        gl.UNSIGNED_BYTE,
        uploadPixels);
    return gl.getError() === gl.NO_ERROR;
}

export function renderFrame(
    canvas,
    width,
    height,
    clearRed,
    clearGreen,
    clearBlue,
    clearAlpha,
    vertexBytesView,
    vertexByteCount,
    batchFirstVertices,
    batchVertexCounts,
    batchBlendModes,
    batchTextureIds) {
    const state = getState(canvas);
    const gl = state.gl;
    const pixelWidth = Math.max(1, width | 0);
    const pixelHeight = Math.max(1, height | 0);

    if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
    }

    const vertices = toFloat32Array(vertexBytesView, vertexByteCount);

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, pixelWidth, pixelHeight);
    gl.disable(gl.DEPTH_TEST);
    gl.disable(gl.CULL_FACE);
    gl.disable(gl.SCISSOR_TEST);
    gl.disable(gl.STENCIL_TEST);
    gl.colorMask(true, true, true, true);
    gl.clearColor(clearRed, clearGreen, clearBlue, clearAlpha);
    gl.clear(gl.COLOR_BUFFER_BIT);

    if (!vertices.length || !batchTextureIds.length) {
        return;
    }

    gl.useProgram(state.program);
    gl.activeTexture(gl.TEXTURE0);
    gl.uniform1i(state.textureLocation, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, state.vertexBuffer);
    gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STREAM_DRAW);
    configureAttributes(gl, state);
    gl.enable(gl.BLEND);

    for (let i = 0; i < batchTextureIds.length; i++) {
        const texture = state.textures.get(batchTextureIds[i]);
        const firstVertex = batchFirstVertices[i] | 0;
        const vertexCount = batchVertexCounts[i] | 0;
        if (!texture || vertexCount <= 0) {
            continue;
        }

        if ((batchBlendModes[i] | 0) === 1) {
            gl.blendFuncSeparate(gl.ONE, gl.ONE, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        } else {
            gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        }

        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.drawArrays(gl.TRIANGLES, firstVertex, vertexCount);
    }
}

export function readPixels(canvas, rgbaPixels, byteCount) {
    const state = getState(canvas);
    const gl = state.gl;
    const pixelWidth = Math.max(1, canvas.width | 0);
    const pixelHeight = Math.max(1, canvas.height | 0);
    const expectedByteCount = pixelWidth * pixelHeight * 4;
    if (expectedByteCount <= 0 || (byteCount | 0) < expectedByteCount) {
        return false;
    }

    const target = toWritableUint8Array(rgbaPixels, expectedByteCount);
    if (!target || target.byteLength < expectedByteCount) {
        return false;
    }

    const source = new Uint8Array(expectedByteCount);
    gl.readPixels(0, 0, pixelWidth, pixelHeight, gl.RGBA, gl.UNSIGNED_BYTE, source);
    if (gl.getError() !== gl.NO_ERROR) {
        return false;
    }

    const rowByteCount = pixelWidth * 4;
    for (let y = 0; y < pixelHeight; y++) {
        const sourceOffset = (pixelHeight - y - 1) * rowByteCount;
        const targetOffset = y * rowByteCount;
        target.set(source.subarray(sourceOffset, sourceOffset + rowByteCount), targetOffset);
    }

    return true;
}

function toWritableUint8Array(memoryView, byteCount) {
    const length = Math.max(0, byteCount | 0);
    if (memoryView instanceof Uint8Array) {
        return memoryView.byteLength === length ? memoryView : memoryView.subarray(0, length);
    }

    if (memoryView && typeof memoryView.getView === "function") {
        const view = memoryView.getView();
        if (view instanceof Uint8Array) {
            return view.byteLength === length ? view : view.subarray(0, length);
        }
    }

    if (memoryView && typeof memoryView.set === "function" && typeof memoryView.length === "number") {
        return memoryView.length === length ? memoryView : memoryView.subarray(0, length);
    }

    return null;
}

function createPremultipliedPixels(source) {
    const pixels = new Uint8Array(source);
    for (let i = 0; i < pixels.length; i += 4) {
        const alpha = pixels[i + 3];
        if (alpha === 255) {
            continue;
        }

        pixels[i] = Math.round(pixels[i] * alpha / 255);
        pixels[i + 1] = Math.round(pixels[i + 1] * alpha / 255);
        pixels[i + 2] = Math.round(pixels[i + 2] * alpha / 255);
    }

    return pixels;
}

function clearGlErrors(gl) {
    for (let i = 0; i < 8; i++) {
        if (gl.getError() === gl.NO_ERROR) {
            return;
        }
    }
}

function contextOptions() {
    return {
        alpha: true,
        depth: false,
        stencil: false,
        antialias: false,
        premultipliedAlpha: true,
        preserveDrawingBuffer: false,
        failIfMajorPerformanceCaveat: false
    };
}

function createState(gl) {
    const program = createProgram(gl);
    const vertexBuffer = gl.createBuffer();
    if (!vertexBuffer) {
        throw new Error("Failed to create EffectViewer WebGL vertex buffer.");
    }

    return {
        gl,
        program,
        vertexBuffer,
        positionLocation: gl.getAttribLocation(program, "a_position"),
        uvLocation: gl.getAttribLocation(program, "a_uv"),
        colorLocation: gl.getAttribLocation(program, "a_color"),
        textureLocation: gl.getUniformLocation(program, "u_texture"),
        textures: new Map()
    };
}

function configureAttributes(gl, state) {
    const stride = FLOATS_PER_VERTEX * BYTES_PER_FLOAT;

    gl.enableVertexAttribArray(state.positionLocation);
    gl.vertexAttribPointer(state.positionLocation, 2, gl.FLOAT, false, stride, 0);

    gl.enableVertexAttribArray(state.uvLocation);
    gl.vertexAttribPointer(state.uvLocation, 2, gl.FLOAT, false, stride, 2 * BYTES_PER_FLOAT);

    gl.enableVertexAttribArray(state.colorLocation);
    gl.vertexAttribPointer(state.colorLocation, 4, gl.FLOAT, false, stride, 4 * BYTES_PER_FLOAT);
}

function createProgram(gl) {
    const vertexShader = compileShader(gl, gl.VERTEX_SHADER, `
        attribute vec2 a_position;
        attribute vec2 a_uv;
        attribute vec4 a_color;
        varying vec2 v_uv;
        varying vec4 v_color;
        void main() {
            v_uv = a_uv;
            v_color = a_color;
            gl_Position = vec4(a_position, 0.0, 1.0);
        }
    `);
    const fragmentShader = compileShader(gl, gl.FRAGMENT_SHADER, `
        precision mediump float;
        varying vec2 v_uv;
        varying vec4 v_color;
        uniform sampler2D u_texture;
        void main() {
            vec4 color = texture2D(u_texture, v_uv) * v_color;
            color.rgb *= v_color.a;
            gl_FragColor = color;
        }
    `);
    const program = gl.createProgram();
    if (!program) {
        throw new Error("Failed to create EffectViewer WebGL shader program.");
    }

    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);
    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const log = gl.getProgramInfoLog(program) || "unknown link error";
        gl.deleteProgram(program);
        throw new Error(`Failed to link EffectViewer WebGL shader program: ${log}`);
    }

    return program;
}

function compileShader(gl, type, source) {
    const shader = gl.createShader(type);
    if (!shader) {
        throw new Error("Failed to create EffectViewer WebGL shader.");
    }

    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader) || "unknown compile error";
        gl.deleteShader(shader);
        throw new Error(`Failed to compile EffectViewer WebGL shader: ${log}`);
    }

    return shader;
}

function getState(canvas) {
    const state = canvas && canvas.__effectViewerWebGl;
    if (!state) {
        throw new Error("EffectViewer WebGL viewport has not been initialized.");
    }

    return state;
}

function toTypedArray(memoryView, count, typedArrayCtor) {
    const length = Math.max(0, count | 0);
    if (length === 0) {
        return new typedArrayCtor(0);
    }

    if (memoryView instanceof typedArrayCtor) {
        return memoryView.length === length ? memoryView : memoryView.subarray(0, length);
    }

    if (memoryView && typeof memoryView.slice === "function") {
        const sliced = memoryView.slice(0, length);
        if (sliced instanceof typedArrayCtor) {
            return sliced;
        }

        return new typedArrayCtor(sliced);
    }

    const target = new typedArrayCtor(length);
    if (memoryView && typeof memoryView.copyTo === "function") {
        memoryView.copyTo(target, 0);
    } else if (memoryView && typeof memoryView.length === "number") {
        target.set(memoryView.slice ? memoryView.slice(0, length) : memoryView);
    }

    return target;
}

function toFloat32Array(memoryView, byteCount) {
    const length = Math.max(0, byteCount | 0);
    if (length === 0) {
        return new Float32Array(0);
    }

    const bytes = toTypedArray(memoryView, length, Uint8Array);
    const alignedByteLength = bytes.byteLength - (bytes.byteLength % BYTES_PER_FLOAT);
    if (alignedByteLength <= 0) {
        return new Float32Array(0);
    }

    if (bytes.byteOffset % BYTES_PER_FLOAT === 0 && alignedByteLength === bytes.byteLength) {
        return new Float32Array(bytes.buffer, bytes.byteOffset, bytes.byteLength / BYTES_PER_FLOAT);
    }

    const alignedBytes = bytes.slice(0, alignedByteLength);
    return new Float32Array(alignedBytes.buffer, alignedBytes.byteOffset, alignedBytes.byteLength / BYTES_PER_FLOAT);
}
