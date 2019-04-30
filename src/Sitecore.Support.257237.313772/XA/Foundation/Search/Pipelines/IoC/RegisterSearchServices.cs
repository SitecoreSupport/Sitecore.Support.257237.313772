using Microsoft.Extensions.DependencyInjection;
using Sitecore.DependencyInjection;
using Sitecore.XA.Foundation.Search.Interfaces;
using Sitecore.XA.Foundation.Search.Services;
using Sitecore.XA.Foundation.Search.Wrappers;


namespace Sitecore.Support.XA.Foundation.Search.Pipelines.IoC
{
  public class RegisterSearchServices : IServicesConfigurator
  {
    public void Configure(IServiceCollection serviceCollection)
    {
      serviceCollection.AddSingleton<ISortingService, SortingService>();
      #region MODIFIED FOR PATCH 257237.313772 Using patches search service.
      serviceCollection.AddSingleton<ISearchService, Sitecore.Support.XA.Foundation.Search.Services.SearchService>();
      #endregion
      serviceCollection.AddSingleton<IScopeService, ScopeService>();
      serviceCollection.AddSingleton<IFacetService, FacetService>();
      serviceCollection.AddSingleton<IContentSearchManager, ContentSearchManager>();
      serviceCollection.AddSingleton<ILinqHelper, LinqHelper>();
      serviceCollection.AddSingleton<IIndexResolver, IndexResolver>();
      serviceCollection.AddSingleton<ISearchContextService, SearchContextService>();
    }
  }
}