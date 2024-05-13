var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<GraphDB.GraphService>(GraphDB.GraphService.Instance);// Registers GraphService as a singleton
builder.Services.AddRazorPages(); // Support for Razor Pages
builder.Services.AddControllers(); // Adds support for API controllers

// Register IHttpClientFactory
builder.Services.AddHttpClient("GraphDB", client =>
{
    client.BaseAddress = new Uri("https://localhost:7211/"); // Base URI of the API
});




// Register IHttpClientFactory
builder.Services.AddHttpClient("GraphDB", client =>
{
    client.BaseAddress = new Uri("https://localhost:7211/"); // Base URI of the API
});
// Add session support
builder.Services.AddDistributedMemoryCache(); // Backing store for sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromSeconds(3600); // or whatever suits your application
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.WithOrigins("https://localhost:7211/") // Replace with the client app's domain
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage(); // Provides detailed error information in development
}

//app.UseHttpsRedirection(); // Redirect HTTP requests to HTTPS
app.UseStaticFiles();

app.UseRouting(); // Enable routing

app.UseSession(); // Activate the session middleware

app.UseAuthorization(); // Apply authorization policies to the request pipeline

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapControllers(); // Map attribute-routed API controllers

    app.Run();

