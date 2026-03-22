using Microsoft.VisualStudio.TestTools.UnitTesting;
using Workbench;
using Workbench.Core;
using Workbench.Core.Voice;

namespace Workbench.Tests;

[TestClass]
public sealed class VoiceTests
{
    [TestMethod]
    public void AudioLimiterHonorsMaxDuration()
    {
        var format = new AudioFormat(16000, 1, 16);
        var limits = AudioLimiter.Calculate(format, TimeSpan.FromMinutes(4), maxBytes: 50 * 1024 * 1024);

        Assert.AreEqual(TimeSpan.FromMinutes(4), limits.MaxDuration);
        Assert.AreEqual(16000 * 240, limits.MaxFrames);
    }

    [TestMethod]
    public void AudioLimiterHonorsMaxBytes()
    {
        var format = new AudioFormat(16000, 1, 16);
        var limits = AudioLimiter.Calculate(format, TimeSpan.FromMinutes(10), maxBytes: 1 * 1024 * 1024);
        var expectedSeconds = (1 * 1024 * 1024) / (double)format.BytesPerSecond;

        Assert.AreEqual(expectedSeconds, limits.MaxDuration.TotalSeconds, 0.01);
        Assert.IsTrue(limits.MaxDuration < TimeSpan.FromMinutes(10));
    }

    [TestMethod]
    public void TranscriptCombinerAddsChunkMarkers()
    {
        var combined = TranscriptCombiner.Combine(new[] { "first", "second" });

        var expected = "[chunk 1]\nfirst\n\n---\n\n[chunk 2]\nsecond";
        Assert.AreEqual(expected, combined);
    }

    [TestMethod]
    public void DocFrontMatterIncludesVoiceSource()
    {
        var source = new DocSourceInfo(
            "voice",
            "Excerpt",
            new DocAudioInfo(16000, 1, "wav"));

        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        var created = DocService.CreateGeneratedDoc(
            repoRoot,
            WorkbenchConfig.Default,
            "spec",
            "Voice doc",
            "# Voice doc\n",
            path: null,
            workItems: new List<string>(),
            codeRefs: new List<string>(),
            tags: new List<string>(),
            related: new List<string>(),
            status: "draft",
            source: source,
            force: false);

        var serialized = File.ReadAllText(created.Path);
        StringAssert.Contains(serialized, "title: Voice doc", StringComparison.Ordinal);
        StringAssert.Contains(serialized, "artifact_type: specification", StringComparison.Ordinal);
        var artifactIdLine = serialized
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith("artifact_id:", StringComparison.Ordinal));
        Assert.IsTrue(artifactIdLine.StartsWith("artifact_id: SPEC-", StringComparison.Ordinal), serialized);
        Assert.IsFalse(artifactIdLine.EndsWith("-0001", StringComparison.Ordinal), serialized);
        Assert.IsFalse(serialized.Contains("source:", StringComparison.Ordinal), serialized);
        StringAssert.Contains(serialized, "domain: VOICE-DOC", StringComparison.Ordinal);
        StringAssert.Contains(serialized, "capability: VOICE-DOC", StringComparison.Ordinal);
        Assert.IsTrue(created.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase), created.Path);
        var fileName = Path.GetFileName(created.Path);
        Assert.IsTrue(fileName.StartsWith("SPEC-", StringComparison.Ordinal), created.Path);
        Assert.IsTrue(fileName.EndsWith(".md", StringComparison.Ordinal), created.Path);
        Assert.IsFalse(fileName.Contains("-0001", StringComparison.Ordinal), created.Path);

        File.Delete(created.Path);
    }

    [TestMethod]
    public void DocPathResolverUsesDocTypeDefaults()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "workbench-tests", Guid.NewGuid().ToString("N"));
        var config = WorkbenchConfig.Default;
        var title = "Voice Doc";

        var created = DocService.CreateGeneratedDoc(
            repoRoot,
            config,
            "spec",
            title,
            "# Voice Doc\n",
            path: null,
            workItems: new List<string>(),
            codeRefs: new List<string>(),
            tags: new List<string>(),
            related: new List<string>(),
            status: "draft",
            source: null,
            force: false);

        Assert.AreEqual(Path.Combine(repoRoot, "specs"), Path.GetDirectoryName(created.Path));
        var createdFileName = Path.GetFileName(created.Path);
        Assert.IsTrue(createdFileName.StartsWith("SPEC-", StringComparison.Ordinal), created.Path);
        Assert.IsTrue(createdFileName.EndsWith(".md", StringComparison.Ordinal), created.Path);

        File.Delete(created.Path);
    }
}
