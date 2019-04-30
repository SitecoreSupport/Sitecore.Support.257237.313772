using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.Sites;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.Multisite;
using Sitecore.XA.Foundation.Search.Extensions;
using Sitecore.XA.Foundation.Search.Models;
using Sitecore.XA.Foundation.Search.Services;
using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
using Sitecore.XA.Foundation.SitecoreExtensions.Utils;

namespace Sitecore.Support.XA.Foundation.Search.Services
{
public class SearchService : ISearchService
    {
        protected ISearchContextService SearchContextService { get; set; }
        protected IMultisiteContext MultisiteContext { get; set; }
        protected ISortingService SortingService { get; set; }
        protected IFacetService FacetService { get; set; }
        protected IIndexResolver IndexResolver { get; set; }

        protected IContext Context { get; }
        protected IBoostingService BoostingService { get; set; }
        public bool IsGeolocationRequest => Context.Request.QueryString.AllKeys.Contains("g");

        public SearchService()
        {
            SearchContextService = ServiceLocator.ServiceProvider.GetService<ISearchContextService>();
            MultisiteContext = ServiceLocator.ServiceProvider.GetService<IMultisiteContext>();
            SortingService = ServiceLocator.ServiceProvider.GetService<ISortingService>();
            FacetService = ServiceLocator.ServiceProvider.GetService<IFacetService>();
            IndexResolver = ServiceLocator.ServiceProvider.GetService<IIndexResolver>();
            Context = ServiceLocator.ServiceProvider.GetService<IContext>();
            BoostingService = ServiceLocator.ServiceProvider.GetService<IBoostingService>();
        }

        public IEnumerable<Item> Search(string query = null, string scopeId = null, string language = null, string sortOrder = null, int pageSize = 20, int offset = 0, Coordinates center = null, string site = null, string itemid = null)
        {
            IQueryable<ContentPage> contentQuery = GetQuery(query, scopeId, language, center, site, itemid);
            contentQuery = SortingService.Order(contentQuery, sortOrder, center, site);
            contentQuery = contentQuery.Skip(offset);
            contentQuery = contentQuery.Take(pageSize);

            IEnumerable<Item> items = contentQuery.Select(r => r.GetItem());
            items = items.Where(i => i != null);

            return items;
        }

        public IQueryable<ContentPage> GetQuery(string query, string scope, string language, Coordinates center, string site, string itemid, out string indexName)
        {
            ISearchIndex searchIndex = IndexResolver.ResolveIndex();
            IList<Item> scopeItems = ItemUtils.Lookup(scope, Context.Database);
            IProviderSearchContext searchContext = searchIndex.CreateSearchContext();
            Item contextItem = GetContextItem(itemid);
            IQueryable<ContentPage> queryable;

            indexName = searchIndex.Name;
            
            IEnumerable<SearchStringModel> model = scopeItems.Select(i => i[Sitecore.XA.Foundation.Search.Constants.ScopeQuery]).SelectMany(SearchStringModel.ParseDatasourceString);
            model = ResolveSearchQueryTokens(contextItem, model);
            
            using (new SiteContextSwitcher(SiteContextFactory.GetSiteContext("shell")))
            {
                queryable = LinqHelper.CreateQuery<ContentPage>(searchIndex.CreateSearchContext(), model);
            }
            #region MODIFIED FOR PATCH 257237.313772 NORMALIZING QUERY
            string normalizedSearchPhrase = NormalizeSearchPhrase(query);
            #endregion
            queryable = queryable.Where(IsGeolocationRequest ? GeolocationPredicate(site) : PageOrMediaPredicate(site));
            queryable = queryable.Where(ContentPredicate(normalizedSearchPhrase));
            queryable = queryable.Where(LanguagePredicate(language));
            queryable = queryable.Where(LatestVersionPredicate());
            queryable = queryable.ApplyFacetFilters(Context.Request.QueryString, center, site);
            queryable = BoostingService.BoostQuery(scopeItems, query, contextItem, queryable);

            return queryable;            
        }
            #region MODIFIED FOR PATCH 257237.313772 NORMALIZE
            protected virtual string NormalizeSearchPhrase(string phrase)
            {
              if (string.IsNullOrWhiteSpace(phrase))
              {
                return string.Empty;
              }
              foreach (string escapeCharacter in GetEscapeCharacterSet())
              {
                phrase = phrase.Replace(escapeCharacter, " ");
              }
              if (string.IsNullOrWhiteSpace(phrase))
              {
                phrase = string.Empty;
              }
              return phrase;
            }

            protected virtual HashSet<string> GetEscapeCharacterSet()
            {
              return new HashSet<string> { "+", "-", "&", "|", "!", "{", "}", "[", "]", "^", "(", ")", "~", ":", ";", ",", "/", @"\", "?", @"""" };
            }
            #endregion
    protected virtual IEnumerable<SearchStringModel> ResolveSearchQueryTokens(Item contextItem, IEnumerable<SearchStringModel> models)
        {
            var resolver = ServiceLocator.ServiceProvider.GetService<ISearchQueryTokenResolver>();
            var searchStringModels = models.ToList();            
            return resolver.Resolve(searchStringModels, contextItem);
        }

        protected virtual IQueryable<ContentPage> GetQuery(string query, string scope, string language, Coordinates center, string site, string itemid)
        {
            string indexName;
            return GetQuery(query, scope, language, center, site, itemid, out indexName);
        }

        protected virtual Expression<Func<ContentPage, bool>> PageOrMediaPredicate(string siteName)
        {
            Item homeItem = SearchContextService.GetHomeItem(siteName);
            if (homeItem == null)
            {
                return PredicateBuilder.False<ContentPage>();
            }

            string homeShortId = homeItem.ID.ToSearchID();
            Expression<Func<ContentPage, bool>> predicate = i => i.RawPath == homeShortId && i.IsSearchable;
            var settingsItem = MultisiteContext.GetSettingsItem(homeItem);
            if (settingsItem != null)
            {
                MultilistField associatedContent = settingsItem.Fields[Sitecore.XA.Foundation.Search.Templates._SearchCriteria.Fields.AssociatedContent];
                if (associatedContent != null)
                {
                    foreach (var id in associatedContent.TargetIDs.Select(i => i.ToSearchID()))
                    {
                        predicate = predicate.Or(i => i.RawPath == id && i.IsSearchable);
                    }
                }

                MultilistField associatedMedia = settingsItem.Fields[Sitecore.XA.Foundation.Search.Templates._SearchCriteria.Fields.AssociatedMedia];
                if (associatedMedia != null)
                {
                    foreach (var shortId in associatedMedia.GetItems().Select(i => i.ID.ToSearchID()))
                    {
                        predicate = predicate.Or(i => i.RawPath == shortId);
                    }
                }
            }
            return predicate;
        }

        protected virtual Expression<Func<ContentPage, bool>> GeolocationPredicate(string siteName)
        {
            Item homeItem = SearchContextService.GetHomeItem(siteName);
            Item siteItem = MultisiteContext.GetSiteItem(homeItem);

            if (homeItem == null || siteItem == null)
            {
                return PredicateBuilder.False<ContentPage>();
            }

            string siteShortId = siteItem.ID.ToSearchID();
            Expression<Func<ContentPage, bool>> predicate = i => i.RawPath == siteShortId && i.IsPoi;
            var settingsItem = MultisiteContext.GetSettingsItem(homeItem);
            if (settingsItem != null)
            {
                MultilistField associatedContent = settingsItem.Fields[Sitecore.XA.Foundation.Search.Templates._SearchCriteria.Fields.AssociatedContent];
                if (associatedContent != null)
                {
                    foreach (var id in associatedContent.TargetIDs.Select(i => i.ToSearchID()))
                    {
                        predicate.Or(i => i.RawPath == id && i.IsPoi);
                    }
                }
            }

            return predicate;
        }

        protected virtual Expression<Func<ContentPage, bool>> ContentPredicate(string content)
        {
            Expression<Func<ContentPage, bool>> predicate = PredicateBuilder.True<ContentPage>();
            if (string.IsNullOrWhiteSpace(content))
            {
                return predicate;
            }

            foreach (string term in content.Split().TrimAndRemoveEmpty())
            {
                string t = term;
                predicate = predicate.And(i => i.AggregatedContent.Contains(t));
            }
            return predicate;
        }

        protected virtual Expression<Func<ContentPage, bool>> LatestVersionPredicate()
        {
            var predicate = PredicateBuilder.True<ContentPage>();
            return predicate.And(i => i.LatestVersion);
        }

        protected virtual Expression<Func<ContentPage, bool>> LanguagePredicate(string language)
        {
            IEnumerable<string> languages = language.ParseLanguages().Select(l => l.Name).ToList();

            if (!languages.Any())
            {
                return PredicateBuilder.True<ContentPage>();
            }

            Expression<Func<ContentPage, bool>> predicate = PredicateBuilder.False<ContentPage>();
            predicate = languages.Aggregate(predicate, (p, l) => p.Or(i => i.Language == l));
            return predicate;
        }

        protected virtual Item GetContextItem(string itemId)
        {
            Item contextItem = null;
            if (ID.IsID(itemId))
            {
                contextItem = Context.Database.GetItem(new ID(itemId));
            }
            return contextItem;
        }
    }
}