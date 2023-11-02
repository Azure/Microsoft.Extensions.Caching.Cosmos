# Changelog

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## <a name="1.6.0"/> 1.6.0 - 2023-11-01

### Added

- [#72](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/72) Increased SDK dependency version for critical fixes


## <a name="1.5.0"/> 1.5.0 - 2023-06-22

### Added

- [#71](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/71) Increased SDK dependency version for critical fixes

## <a name="1.4.0"/> 1.4.0 - 2022-10-08

### Added

- [#69](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/69) Increased SDK dependency version for critical fixes

## <a name="1.3.0"/> 1.3.0 - 2022-06-02

### Added

- [#63](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/63) Increased SDK dependency version and Microsoft.Extensions.Caching.Abstractions to 6.X to align with NET Core 3.1 deprecation

### Fixed

- [#62](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/62) Fixed error text to point to correct missing property when deserializing session object

## <a name="1.2.0"/> 1.2.0 - 2022-02-23

### Added

- [#59](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/59) Increased SDK dependency version

## <a name="1.1.0"/> 1.1.0 - 2021-10-28

### Added

- [#58](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/58) Added support for IOptionsMonitor


## <a name="1.0.1"/> 1.0.1 - 2021-08-24

### Fixed

- [#52](https://github.com/Azure/Microsoft.Extensions.Caching.Cosmos/pull/52) Fixed DefaultTimeToLiveInMs handling when creating container

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
