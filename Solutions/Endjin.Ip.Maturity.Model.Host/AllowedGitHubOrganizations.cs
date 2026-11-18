namespace Endjin.Ip.Maturity.Model.Host
{
    #region Using Directives

    using Microsoft.Extensions.Configuration;

    using System;
    using System.Collections.Frozen;
    using System.Collections.Generic;
    using System.Linq;

    #endregion

    /// <summary>
    /// The GitHub organizations (or users) whose repositories a deployment of the Host evaluates
    /// IMMs for, as set by the <c>Imm:AllowedGitHubOrganizations</c> configuration setting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Host's <c>appsettings.json</c> sets the list, as a JSON array, to endjin's open source
    /// estate. A deployment can replace the whole list with an app setting (or environment variable)
    /// named <c>Imm__AllowedGitHubOrganizations</c> whose value is the names separated by commas,
    /// semicolons or white space. A value like this takes precedence over an array, so an override
    /// never merges with the array's entries. The name <c>*</c> allows every organization.
    /// </para>
    /// <para>
    /// A list that is missing or empty is a configuration error, so that a deployment never
    /// silently refuses every badge or allows every organization.
    /// </para>
    /// <para>
    /// GitHub names are not case sensitive, so neither is the comparison.
    /// </para>
    /// </remarks>
    public sealed class AllowedGitHubOrganizations
    {
        /// <summary>
        /// The configuration key of the list of allowed organizations.
        /// </summary>
        public const string ConfigurationKey = "Imm:AllowedGitHubOrganizations";

        /// <summary>
        /// The name of the app setting or environment variable that overrides the list.
        /// </summary>
        public const string AppSettingName = "Imm__AllowedGitHubOrganizations";

        /// <summary>
        /// The name that allows every organization.
        /// </summary>
        public const string AnyOrganization = "*";

        private static readonly char[] Separators = [',', ';', ' ', '\t', '\r', '\n'];

        private readonly FrozenSet<string>? organizations;

        private AllowedGitHubOrganizations(FrozenSet<string>? organizations)
        {
            this.organizations = organizations;
        }

        /// <summary>
        /// Gets an instance that allows every organization.
        /// </summary>
        public static AllowedGitHubOrganizations Any { get; } = new(null);

        /// <summary>
        /// Gets a value indicating whether every organization is allowed.
        /// </summary>
        public bool AllowsAny => this.organizations is null;

        /// <summary>
        /// Gets the allowed organizations, in no particular order, or an empty list when every
        /// organization is allowed.
        /// </summary>
        public IReadOnlyCollection<string> Organizations => this.organizations?.Items ?? [];

        /// <summary>
        /// Creates an instance that allows the given organizations, or every organization if they
        /// include <see cref="AnyOrganization"/>.
        /// </summary>
        /// <param name="organizations">The organizations to allow.</param>
        /// <returns>The allowed organizations.</returns>
        /// <exception cref="ArgumentException">No organizations are given.</exception>
        public static AllowedGitHubOrganizations Only(IEnumerable<string> organizations)
        {
            ArgumentNullException.ThrowIfNull(organizations);
            string[] names = organizations
                .SelectMany(o => (o ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();
            if (names.Length == 0)
            {
                throw new ArgumentException("At least one organization must be given.", nameof(organizations));
            }

            return names.Contains(AnyOrganization)
                ? Any
                : new(names.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reads the allowed organizations from the Host's configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>The allowed organizations.</returns>
        /// <exception cref="InvalidOperationException">
        /// The configuration does not list any organizations.
        /// </exception>
        public static AllowedGitHubOrganizations FromConfiguration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            IConfigurationSection section = configuration.GetSection(ConfigurationKey);

            // A single value (from an app setting) replaces the array (from appsettings.json) rather
            // than merging with it entry by entry, as configuration arrays otherwise would.
            IEnumerable<string> names = !string.IsNullOrWhiteSpace(section.Value)
                ? [section.Value]
                : section.GetChildren().Select(c => c.Value ?? string.Empty);

            try
            {
                return Only(names);
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException(
                    $"The '{ConfigurationKey}' setting does not list any GitHub organizations. List them in appsettings.json, " +
                    $"or set the app setting '{AppSettingName}' to the names separated by commas, or to '{AnyOrganization}' to allow every organization.");
            }
        }

        /// <summary>
        /// Determines whether IMMs from the given organization's repositories are evaluated.
        /// </summary>
        /// <param name="organization">The organization (or user) that owns the repository.</param>
        /// <returns>True if the organization is allowed.</returns>
        public bool IsAllowed(string organization) =>
            this.organizations is null || (!string.IsNullOrEmpty(organization) && this.organizations.Contains(organization));
    }
}
