using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text;
using System.Text.Json;

namespace TradeFlowGuardian.Api.Formatters;

/// <summary>
/// Allows ASP.NET to deserialize JSON bodies sent with Content-Type: text/plain.
/// TradingView webhooks do not set Content-Type: application/json.
/// </summary>
public class TextPlainJsonInputFormatter : InputFormatter
{
    public TextPlainJsonInputFormatter()
    {
        SupportedMediaTypes.Add("text/plain");
    }

    protected override bool CanReadType(Type type) => true;

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        using var reader = new StreamReader(context.HttpContext.Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        try
        {
            var result = JsonSerializer.Deserialize(body, context.ModelType,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });
            return InputFormatterResult.Success(result);
        }
        catch (JsonException ex)
        {
            context.ModelState.AddModelError(context.FieldName, ex.Message);
            return InputFormatterResult.Failure();
        }
    }
}
