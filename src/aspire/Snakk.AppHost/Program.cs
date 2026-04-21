var builder = DistributedApplication.CreateBuilder(args);

// --- Services ----------------------------------------------

var realtime = builder.AddProject<Projects.Snakk_Realtime>("snakk-realtime")
    .WithEndpoint("https", e => { e.Port = 17103; });

var api = builder.AddProject<Projects.Snakk_Api>("snakk-api")
    .WithEndpoint("https", e => { e.Port = 17101; })
    .WithEnvironment("RestPort", "17102")
    .WithEnvironment("Realtime__BaseUrl", realtime.GetEndpoint("https"))
    .WaitFor(realtime);

var worker = builder.AddProject<Projects.Snakk_Worker>("snakk-worker")
    .WithEnvironment("Realtime__BaseUrl", realtime.GetEndpoint("https"));

// --- Apps --------------------------------------------------

var setup = builder.AddProject<Projects.Snakk_Setup>("snakk-setup")
    .WithEndpoint("https", e => { e.Port = 17113; });

var web = builder.AddProject<Projects.Snakk_Web>("snakk-web")
    .WithEndpoint("https", e => { e.Port = 17110; })
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    .WaitFor(api);

var auth = builder.AddProject<Projects.Snakk_Auth>("snakk-auth")
    .WithEndpoint("https", e => { e.Port = 17111; })
    .WithEnvironment("ApiBaseUrl", api.GetEndpoint("https"))
    .WaitFor(api);

var admin = builder.AddProject<Projects.Snakk_Admin>("snakk-admin")
    .WithEndpoint("https", e => { e.Port = 17112; })
    .WithEnvironment("SnakkApi__BaseUrl", api.GetEndpoint("https"))
    .WaitFor(api);

// Wire API's CORS origin to Web's actual URL
api.WithEnvironment("WebClientUrl", web.GetEndpoint("https"));

// --- Gateway (entry point) ---------------------------------

var gateway = builder.AddProject<Projects.Snakk_Gateway>("snakk-gateway")
    .WithEndpoint("https", e => { e.Port = 17100; })
    .WithExternalHttpEndpoints()
    // Override YARP cluster addresses with actual service endpoints
    .WithEnvironment("ReverseProxy__Clusters__web-cluster__Destinations__web__Address", web.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__admin-cluster__Destinations__admin__Address", admin.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__auth-cluster__Destinations__auth__Address", auth.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__realtime-cluster__Destinations__realtime__Address", realtime.GetEndpoint("https"))
    .WithEnvironment("ReverseProxy__Clusters__setup-cluster__Destinations__setup__Address", setup.GetEndpoint("https"))
    .WaitFor(web)
    .WaitFor(admin)
    .WaitFor(auth)
    .WaitFor(realtime);

builder.Build().Run();
