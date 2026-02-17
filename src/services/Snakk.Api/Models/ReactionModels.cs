namespace Snakk.Api.Models;

using Snakk.Shared.Enums;
using System.Text.Json.Serialization;

public record ToggleReactionRequest([property: JsonPropertyName("type")] ReactionTypeEnum Type);
