using EuroGlobe.Accounts;
using EuroGlobe.Api.Controllers.Demo;
using EuroGlobe.Jobs;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAccountsModule();
builder.Services.AddEuroGlobeJobs(builder.Configuration, builder.Environment);
builder.Services.AddDemoAdminEndpoints(builder.Configuration, builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
