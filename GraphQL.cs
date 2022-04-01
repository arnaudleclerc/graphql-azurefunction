using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using GraphQL.NewtonsoftJson;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

[assembly: Microsoft.Azure.WebJobs.Hosting.WebJobsStartup(typeof(GraphQL.Function.Startup))]

namespace GraphQL.Function;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder
            .Services
            .AddTransient<GraphQLQuery>()
            .AddTransient(sp => new GraphQLServer { ServiceProvider = sp });
    }
}

public class GraphQLFunction
{
    private readonly GraphQLServer _graphQLServer;

    public GraphQLFunction(GraphQLServer graphQLServer) => _graphQLServer = graphQLServer;

    [FunctionName(nameof(GraphQLQuery))]
    public async Task<HttpResponseMessage> GraphQLQuery([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "graphql")] HttpRequest request)
    {
        var query = request.Query["query"];
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(await _graphQLServer.ExecuteQueryAsync(query),
            System.Text.Encoding.UTF8,
            "application/json")
        };
    }
}

public class GraphQLServer
{
    private readonly string _typeDefinition = @"
        type Project {
            id: ID,
            status: ProjectType
        }

        enum ProjectType {
            Completed,
            Failed
        }

        type Query {
            projects: [Project]
        }
    ";

    public IServiceProvider ServiceProvider { get; set; }

    public async Task<string> ExecuteQueryAsync(string query)
    {
        var schema = Schema.For(_typeDefinition, builder =>
        {
            builder.ServiceProvider = ServiceProvider;

            builder.Types.Include<GraphQLQuery>("Query");
            builder.Types.Include<ProjectType>(); // With this line, an error comes back : Error trying to resolve field 'status'.
            //builder.Types.Include<ProjectType>("ProjectType"); // With this line, the type is correctly resolved.
        });

        return await schema.ExecuteAsync(new GraphQLSerializer(), options => options.Query = query);
    }
}

public class GraphQLQuery
{
    [GraphQLMetadata("projects")]
    public IEnumerable<Project> GetProjects()
    {
        return new[] {
            new Project {
                Id = Guid.NewGuid(),
                Status = ProjectType.Completed
            }
        };
    }
}

public class Project
{
    public Guid Id { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public ProjectType Status { get; set; }
}

public enum ProjectType
{
    [EnumMember(Value = "Completed")]
    Completed,
    [EnumMember(Value = "Failed")]
    Failed
}