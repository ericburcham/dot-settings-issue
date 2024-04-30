// -----------------------------------------------------------------------
// <copyright file="Configuration.cs" company="Enterprise Products Partners L.P.">
// For copyright details, see the COPYRIGHT file in the root of this repository.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.Linq;
using Nuke.Common.Tooling;

[TypeConverter(typeof(TypeConverter<Configuration>))]
public class Configuration : Enumeration
{
    public static Configuration Debug = new() {Value = nameof(Debug)};
    public static Configuration Release = new() {Value = nameof(Release)};

    public static implicit operator string(Configuration configuration) => configuration.Value;
}