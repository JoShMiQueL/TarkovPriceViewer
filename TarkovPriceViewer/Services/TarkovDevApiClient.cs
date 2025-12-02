using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TarkovPriceViewer.Services
{
    public interface ITarkovDevApiClient
    {
        Task<string> GetItemsSnapshotJsonAsync(string lang, string gameMode, CancellationToken cancellationToken = default);
    }

    public class TarkovDevApiClient : ITarkovDevApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private const string GraphQlEndpoint = "https://api.tarkov.dev/graphql";

        public TarkovDevApiClient(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<string> GetItemsSnapshotJsonAsync(string lang, string gameMode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                throw new ArgumentException("lang must not be null or empty", nameof(lang));
            }

            if (string.IsNullOrWhiteSpace(gameMode))
            {
                throw new ArgumentException("gameMode must not be null or empty", nameof(gameMode));
            }

            string query = BuildItemsSnapshotQuery(lang, gameMode);

            var payload = new Dictionary<string, string>
            {
                { "query", query }
            };

            using HttpClient client = _httpClientFactory.CreateClient();

            HttpResponseMessage response = await client
                .PostAsJsonAsync(GraphQlEndpoint, payload, cancellationToken)
                .ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return json;
        }

        private static string BuildItemsSnapshotQuery(string lang, string gameMode)
        {
            // lang and gameMode are GraphQL enums in tarkov.dev (e.g. en, regular), so they must not be quoted
            return $@"{{
  items(lang:{lang}, gameMode:{gameMode}) {{
    id
    name
    shortName
    normalizedName
    iconLink
    width
    height
    types
    lastLowPrice
    avg24hPrice
    updated
    fleaMarketFee
    sellFor {{
      currency
      priceRUB
      vendor {{
        name
        ... on TraderOffer {{
          minTraderLevel
        }}
      }}
    }}
    bartersFor {{
      trader {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
      taskUnlock {{
        name
      }}
    }}
    bartersUsing {{
      trader {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
    }}
    craftsFor {{
      station {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
    }}
    craftsUsing {{
      station {{
        name
        levels {{
          level
        }}
      }}
      requiredItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
      rewardItems {{
        item {{
          id
          name
        }}
        count
        quantity
      }}
    }}
    usedInTasks {{
      id
      name
      minPlayerLevel
      trader {{
        name
      }}
      map {{
        name
      }}
      traderLevelRequirements {{
        level
      }}
      objectives {{
        id
        description
        maps {{
          name
        }}
        type
        ... on TaskObjectiveItem {{
          id
          count
          type
          foundInRaid
          items {{
            id
            name
          }}
        }}
      }}
    }}
  }}
  hideoutStations {{
    name
    levels {{
      id
      level
      itemRequirements {{
        id
        item {{
          id
          name
        }}
        count
      }}
    }}
  }}
}}";
        }
    }
}
