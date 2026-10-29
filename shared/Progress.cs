namespace BloxCode.Core;

public enum BootstrapPhase { Connecting, Checking, Downloading, Installing, Launching, Done }

/// <summary>What the launch window shows: a phase (which picks Bit's mood), a status line and optional progress.</summary>
public sealed record BootstrapProgress(BootstrapPhase Phase, string Status, double? Fraction = null, string? Detail = null);
