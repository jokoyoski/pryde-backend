using System.Text.Json.Serialization;
using Pryde.Domain.Enums;

namespace Pryde.Contracts.ResponseModels;

public class WorkflowResponseDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowNextAction? NextAction { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkflowActor? RequiredActor { get; set; }
}
