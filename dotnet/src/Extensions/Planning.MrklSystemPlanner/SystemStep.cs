﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Planning.MrklSystem;

/// <summary>
/// A step in a Mrkl system plan.
/// </summary>
public class SystemStep
{
    /// <summary>
    /// Gets or sets the step number.
    /// </summary>
    [JsonPropertyName("thought")]
    public string? Thought { get; set; }

    /// <summary>
    /// Gets or sets the action of the step
    /// </summary>
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    /// <summary>
    /// Gets or sets the input for the action
    /// </summary>
    [JsonPropertyName("action_input")]
    public string? ActionInput { get; set; }

    /// <summary>
    /// Gets or sets the output of the action
    /// </summary>
    [JsonPropertyName("observation")]
    public string? Observation { get; set; }

    /// <summary>
    /// Gets or sets the output of the system
    /// </summary>
    [JsonPropertyName("final_answer")]
    public string? FinalAnswer { get; set; }

    /// <summary>
    /// The raw response from the action
    /// </summary>
    [JsonPropertyName("original_response")]
    public string? OriginalResponse { get; set; }
}