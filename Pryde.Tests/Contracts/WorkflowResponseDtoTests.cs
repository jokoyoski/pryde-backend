using System.Text.Json;
using System.Text.Json.Serialization;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Enums;

namespace Pryde.Tests.Contracts;

public class WorkflowResponseDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    [Fact]
    public void QueryResponseOmitsUnsetWorkflowFields()
    {
        var response = new TripDetailsResponseDto
        {
            TripId = Guid.NewGuid(),
            Status = TripStatus.Scheduled
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.DoesNotContain("\"nextAction\"", json);
        Assert.DoesNotContain("\"requiredActor\"", json);
    }

    [Fact]
    public void CommandResponseIncludesExplicitNoneAction()
    {
        var response = new TripBookingResponseDto
        {
            BookingId = Guid.NewGuid(),
            Status = BookingStatus.Declined,
            NextAction = WorkflowNextAction.None,
            RequiredActor = WorkflowActor.None
        };

        var json = JsonSerializer.Serialize(response, JsonOptions);

        Assert.Contains("\"nextAction\":\"None\"", json);
        Assert.Contains("\"requiredActor\":\"None\"", json);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(
            JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
