# Changelog

## [1.2.0] - 2025-01-16
### Changed
- Updated version to 1.2.0
- Package metadata improvements
### Added
- `AddNotificationPublishers` extension method for registering notification publishers and handlers
- Support for concurrent and sequential notification handler execution
- Enhanced notification publishing with configurable execution modes

## [1.1.0] - 2025-04-25
### Added
- INotification
- INotificationHandler<TNotification>
- INotificationPublisher<TNotification>
- NotificationPublisher<TNotification>
### Changed
- ServiceCollectionExtensions

## [1.0.1] - 2025-04-22
### Changed
- Updated `AddFeatureActions` service extension method

## [1.0.0] - 2025-04-01
### Added
- IRequest<TResponse>
- IRequest
- IRequestHandler<TRequest, TResponse>
- IFeatureAction<TRequest>
- IFeatureAction<TRequest, TResponse>
- BaseFeatureAction<TRequest, TResponse>
- FeatureAction<TRequest, TResponse>
- ServiceCollectionExtensions