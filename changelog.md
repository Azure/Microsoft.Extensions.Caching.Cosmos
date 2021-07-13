# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## <a name="1.0.0"/> 1.0.0 - 2021-07-14

### Added

- [#43](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/43) Adding Diagnostics capture APIs
- [#44](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/44) & [#46](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/46) Bump dependencies versions to latest stable before GA release

## <a name="1.0.0-preview6"/> 1.0.0-preview6 - 2021-03-08

### Added

- [#41](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/41) EnableContentResponseOnWrite to reduce network usage when not needed

### Fixed

- [#38](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/38) Fixed Resource Not Found when removing an entry that does not exist

## <a name="1.0.0-preview5"/> 1.0.0-preview5 - 2020-10-13

### Added

- [#31](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/31) Added support for Gremlin accounts
- [#33](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/33) Added support for chaining SlidingExpiration and AbsoluteExpiration

## <a name="1.0.0-preview4"/> 1.0.0-preview4 - 2020-07-27

### Added

- [#28](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/28) Added CosmosCacheOptions.RetrySlidingExpirationUpdates to disable write retries during Get operations for highly concurrent stale item scenarios.

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
