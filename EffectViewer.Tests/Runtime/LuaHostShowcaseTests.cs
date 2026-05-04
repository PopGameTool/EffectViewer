using System;
using System.Collections.Generic;
using EffectViewer.Projects;
using EffectViewer.Rendering;
using EffectViewer.Runtime;
using EffectViewer.Runtime.Lua;
using EffectViewer.Tests.TestUtilities;

namespace EffectViewer.Tests.Runtime;

public sealed class LuaHostShowcaseTests
{
    [Fact]
    public void RunRegistersShowcaseCallbacksAndCapturesDrawFrame()
    {
        using TempDirectory temp = new();
        using EffectWorld world = CreateWorld(temp);
        LuaHost host = new(world);
        List<string> liveLogs = [];

        LuaRunResult result = host.Run("""
            scene.log("boot")

            local context = { updates = 0 }

            function context:update(delta, elapsed, frame)
              self.updates = self.updates + 1
              scene.log("update " .. frame)
            end

            function context:draw(g, elapsed, frame)
              g:set_color(255, 0, 0, 255)
              g:fill_rect(0, 0, 2, 2)
              scene.log("draw " .. self.updates)
            end

            scene.regist(context)
            """, liveLogs.Add);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Logs));
        Assert.NotNull(result.FrameProvider);
        Assert.Contains("boot", result.Logs);

        RenderFrame frame = result.FrameProvider.GetFrame(0.02);

        Assert.Single(frame.Meshes);
        Assert.Contains(liveLogs, log => log.StartsWith("update ", StringComparison.Ordinal));
        Assert.Contains(liveLogs, log => log.StartsWith("draw ", StringComparison.Ordinal));
    }

    [Fact]
    public void RunReportsLuaValidationErrorsWithoutKeepingFrameProvider()
    {
        using TempDirectory temp = new();
        using EffectWorld world = CreateWorld(temp);
        LuaHost host = new(world);

        LuaRunResult result = host.Run("""
            scene.regist({ update = 42 })
            """);

        Assert.False(result.Success);
        Assert.Null(result.FrameProvider);
        Assert.Contains(result.Logs, log => log.Contains("'update' must be a function", StringComparison.Ordinal));
    }

    [Fact]
    public void SceneApiRecordsShowcaseObjectsOnSuccessfulScriptRun()
    {
        using TempDirectory temp = new();
        using EffectWorld world = CreateWorld(temp, manifest =>
        {
            manifest.Particles.Add(new EffectAsset { Id = "spark", Path = "assets/particles/spark.xml" });
        });
        LuaHost host = new(world);

        LuaRunResult result = host.Run("""
            scene.log(tostring(scene.resource_exist("spark", "particle")))
            scene.clear()
            """);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Logs));
        Assert.Contains("true", result.Logs);
        Assert.Contains("scene cleared", result.Logs);
        Assert.Empty(result.SceneObjects);
    }

    private static EffectWorld CreateWorld(TempDirectory temp, Action<ProjectManifest>? configure = null)
    {
        ProjectManifest manifest = new();
        configure?.Invoke(manifest);
        EffectWorld world = new();
        world.LoadProject(new EffectProject(temp.Path, manifest));
        return world;
    }
}
