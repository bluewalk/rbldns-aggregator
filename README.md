# RBL DNS Aggregator
Provides a simple DNS server for RBL blacklisting that combines different RBL blacklists into one.

## Installation
Run the docker image as followed
```
docker run -d --name rbldnsaggregator bluewalk/rbldnsaggregator [-e ...]
```
You can specify configuration items using environment variables as displayed below, e.g.
```
docker run -d --name rbldnsaggregator bluewalk/rbldnsaggregator -e DNS_SUFFIX=mydomain.com -e UPSTREAM_DNS=1.1.1.1
```

## Configuration
|Environment variable (docker) | Description | Default when empty |
|-|-|-|
| DNS_SUFFIX | Suffix for DNS queries | `local` |
| UPSTREAM_DNS | Upstream DNS server | `8.8.8.8` |
| PORT | Port for the DNS server | `53` |
| RBL_LIST | Comma or semicolumn separated list of RBL servers | `bl.spamcop.net` |


## Uninstall
1. Stop the running container
2. Delete container if not started with `--rm`
3. Delete image
