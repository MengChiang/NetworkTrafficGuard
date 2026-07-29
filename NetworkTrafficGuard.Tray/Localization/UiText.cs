namespace NetworkTrafficGuard.Tray.Localization;

public sealed record UiText
{
    public required string Settings { get; init; }

    public required string ShowWindow { get; init; }

    public required string ExitApplication { get; init; }

    public required string AppTitle { get; init; }

    public required string WifiLabel { get; init; }

    public required string SecondaryConnectionLabel { get; init; }

    public required string PriorityTitle { get; init; }

    public required string MoveUp { get; init; }

    public required string MoveDown { get; init; }

    public required string ShowColumn { get; init; }

    public required string AlertColumn { get; init; }

    public required string PriorityColumn { get; init; }

    public required string NetworkColumn { get; init; }

    public required string GatewayColumn { get; init; }

    public required string InterfaceColumn { get; init; }

    public required string TypeColumn { get; init; }

    public required string RealtimeTraffic { get; init; }

    public required string SelectNetworkPromptTitle { get; init; }

    public required string SelectNetworkPromptDetail { get; init; }

    public required string SettingsTitle { get; init; }

    public required string SettingsDescription { get; init; }

    public required string ExistingNetworkColumn { get; init; }

    public required string DisplayNameColumn { get; init; }

    public required string LanguageLabel { get; init; }

    public required string AllowAdapterChanges { get; init; }

    public required string AllowRouteChanges { get; init; }

    public required string AlertSettingsTitle { get; init; }

    public required string AlertThresholdLabel { get; init; }

    public required string AlertThresholdUnit { get; init; }

    public required string Save { get; init; }

    public required string Cancel { get; init; }

    public required string InUse { get; init; }

    public required string Available { get; init; }

    public required string Connected { get; init; }

    public required string NotConnected { get; init; }

    public required string Disabled { get; init; }

    public required string NotPresent { get; init; }

    public required string Unknown { get; init; }

    public required string Updating { get; init; }

    public required string InterfaceFormat { get; init; }

    public required string GatewayNotDetected { get; init; }

    public required string NoPrimaryLine { get; init; }

    public required string PrimaryLineFormat { get; init; }

    public required string AdapterEnabledNotice { get; init; }

    public required string AdapterDryRunNotice { get; init; }

    public required string SettingsSaved { get; init; }

    public required string RouteIdle { get; init; }

    public required string RoutePrioritySaved { get; init; }

    public required string RoutePriorityApplied { get; init; }

    public required string RoutePriorityApplyFailedFormat { get; init; }

    public required string NameSavedNotice { get; init; }

    public required string EnableAction { get; init; }

    public required string DisableAction { get; init; }

    public required string WifiUpdatingFormat { get; init; }

    public required string AdapterStateMismatchFormat { get; init; }

    public required string TrafficAlertTitle { get; init; }

    public required string TrafficAlertMessageFormat { get; init; }

    public required string AlertEnabledNoticeFormat { get; init; }

    public required string AlertDisabledNoticeFormat { get; init; }
}
