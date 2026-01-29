namespace Snakk.Api.Models;

using Snakk.Domain.ValueObjects;
using System.Text.Json.Serialization;

public record ToggleReactionRequest([property: JsonPropertyName("type")] ReactionType Type);
