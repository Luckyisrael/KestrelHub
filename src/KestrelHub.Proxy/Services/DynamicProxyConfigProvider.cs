using KestrelHub.Proxy.Services;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace KestrelHub.Proxy.Services;

public class DynamicProxyConfigProvider : IProxyConfigProvider
{
    private readonly IRouteStore _routeStore;
    private volatile DynamicConfigSnapshot _snapshot;

    public DynamicProxyConfigProvider(IRouteStore routeStore)
    {
        _routeStore = routeStore;
        _snapshot = new DynamicConfigSnapshot([], []);
    }

    public IProxyConfig GetConfig() => _snapshot;

    public async Task ReloadAsync()
    {
        var routes = await _routeStore.GetAllActiveRoutesAsync();

        var yarpRoutes = new List<RouteConfig>();
        var yarpClusters = new List<ClusterConfig>();

        foreach (var route in routes)
        {
            var clusterId = $"cluster-{route.DeploymentId:N}";

            yarpRoutes.Add(new RouteConfig
            {
                RouteId = $"route-{route.Id:N}",
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Hosts = [route.Domain]
                }
            });

            yarpClusters.Add(new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["dest1"] = new DestinationConfig
                    {
                        Address = $"http://localhost:{route.TargetPort}"
                    }
                }
            });
        }

        var oldSnapshot = _snapshot;
        _snapshot = new DynamicConfigSnapshot(yarpRoutes, yarpClusters);
        oldSnapshot.SignalChange();
    }

    private sealed class DynamicConfigSnapshot : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public DynamicConfigSnapshot(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public Microsoft.Extensions.Primitives.IChangeToken ChangeToken { get; }

        public void SignalChange() => _cts.Cancel();
    }
}
