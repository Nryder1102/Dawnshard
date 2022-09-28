using DragaliaAPI.Models;
using DragaliaAPI.Models.Database;
using MessagePack.Resolvers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddMvc().AddMvcOptions(option =>
{
    // Must use ContractlessResolver because the DefaultResolver doesn't like serializing the generic BaseResponse<T> 
    // record, even when it is properly annotated with the MessagePackObject decorator.
    option.OutputFormatters.Add(new DragaliaAPI.CustomMessagePackOutputFormatter(ContractlessStandardResolver.Options));
    option.InputFormatters.Add(new DragaliaAPI.CustomMessagePackInputFormatter(ContractlessStandardResolver.Options));
});

builder.Services.AddDbContext<ApiContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection")));


builder.Services
    .AddSingleton<ISessionService, SessionService>()
    .AddScoped<IDeviceAccountService, DeviceAccountService>()
    .AddScoped<IApiRepository, ApiRepository>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }