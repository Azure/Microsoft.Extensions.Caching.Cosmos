# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

## <a name="1.0.0-preview4"/> 1.0.0-preview4 - 2020-07-27

### Fixed

- [#28](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/28) Added CosmosCacheOptions.RetrySlidingExpirationUpdates to avoid bottlenecks on high concurrent read operations

## <a name="1.0.0-preview3"/> 1.0.0-preview3 - 2020-07-20

### Fixed

- [#25](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/25) Added ConfigureAwait to avoid locks when using sync APIs

## <a name="1.0.0-preview2"/> 1.0.0-preview2 - 2020-06-01

### Fixed

- [#23](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/23) Fixed null ttl attribute value on no expiration

## <a name="1.0.0-preview"/> 1.0.0-preview - 2019-01-09

### Added

- [#13](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/13) Bumping dependency to SDK 3.5.0 and adding Consistency readme
- [#16](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/16) Do not update \_ts on reads for absolute expirations
- [#18](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/18) Added YAML files for release and CSPROJ related changes

## <a name="0.0.1"/> 0.0.1 - 2019-09-19

- Initial code release.
