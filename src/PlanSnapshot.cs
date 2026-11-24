// Coat — Epicteta Lupercal
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Coat;

// Keep a single immutable copy of the encoded image across undo states. Position,
// opacity and other editable metadata still belong to each individual snapshot.
internal readonly record struct PlanSnapshot(string Json, string? ImageData)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                type =>
                {
                    if (type.Type == typeof(ReferenceImage))
                        type.Properties.Single(property => property.Name == nameof(ReferenceImage.Data)).ShouldSerialize = (_, _) => false;
                }
            }
        }
    };

    internal static PlanSnapshot Capture(Plan plan) =>
        new(JsonSerializer.Serialize(plan, Options), plan.Background?.Data);

    internal Plan Restore()
    {
        var plan = JsonSerializer.Deserialize<Plan>(Json, Options)!;
        if (plan.Background != null) plan.Background.Data = ImageData!;
        return Plan.Validate(plan);
    }
}
