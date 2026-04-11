// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Search;
using Contracts.Media.Trending;

namespace Contracts;

public static class ProviderExtensions
{
    extension(WatchlistProvider provider)
    {
        public MediaProvider AsMediaProvider()
        {
            return provider switch
            {
                WatchlistProvider.Plex => MediaProvider.Plex,
            };
        }
    }
    extension(MediaSearchProvider provider)
    {
        public MediaProvider AsMediaProvider()
        {
            return provider switch
            {
                MediaSearchProvider.Plex => MediaProvider.Plex,
            };
        }
    }
    extension(MediaRecommendationProvider provider)
    {
        public MediaProvider AsMediaProvider()
        {
            return provider switch
            {
                MediaRecommendationProvider.Tmdb => MediaProvider.Tmdb,
            };
        }
    }
}
