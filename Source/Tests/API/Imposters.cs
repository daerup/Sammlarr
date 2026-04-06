using Contracts.DI;
using Contracts.Media;
using Contracts.Media.Search;
using Imposter.Abstractions;

[assembly: GenerateImposter(typeof(IKeyedServiceIndex<MediaProvider, IMediaSearchService>))]
