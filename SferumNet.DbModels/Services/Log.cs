﻿using System.ComponentModel.DataAnnotations.Schema;
using SferumNet.DbModels.Common;
using SferumNet.DbModels.Enum;

namespace SferumNet.DbModels.Services;

public class Log : Entity
{
    public EventType Type { get; set; }
    public DateTime DateTime { get; set; }
    public string Message { get; set; } = string.Empty;

    [ForeignKey("IdScenario")] public Job? Scenario { get; set; }
    public long? IdScenario { get; set; }
}