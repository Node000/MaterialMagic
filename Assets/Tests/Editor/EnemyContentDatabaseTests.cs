using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class EnemyContentDatabaseTests
{
    [Test]
    public void RuntimeEnemyData_IsIndependentFromDefinitionData()
    {
        EnemyDefinition definition = ScriptableObject.CreateInstance<EnemyDefinition>();
        definition.SetData(new EnemyData
        {
            numericId = 1,
            string_id = "enemy_test",
            maxHealth = 10,
            phases = new[]
            {
                new EnemyPhaseData
                {
                    phase = 0,
                    intentGroups = new[]
                    {
                        new EnemyIntentGroupData
                        {
                            id = 1,
                            intents = new[] { new EnemyIntentData { actionType = EnemyActionType.Attack, value = 3 } }
                        }
                    }
                }
            }
        });

        EnemyData first = definition.CreateRuntimeData();
        first.phases[0].intentGroups[0].intents[0].value = 99;
        EnemyData second = definition.CreateRuntimeData();

        Assert.That(second.phases[0].intentGroups[0].intents[0].value, Is.EqualTo(3));
        Object.DestroyImmediate(definition);
    }

    [Test]
    public void Database_ReturnsDefinitionsByStableNumericId()
    {
        EnemyDefinition first = ScriptableObject.CreateInstance<EnemyDefinition>();
        first.SetData(new EnemyData { numericId = 1, string_id = "enemy_one", maxHealth = 10 });
        EnemyDefinition second = ScriptableObject.CreateInstance<EnemyDefinition>();
        second.SetData(new EnemyData { numericId = 2, string_id = "enemy_two", maxHealth = 20 });
        GameContentDatabase database = ScriptableObject.CreateInstance<GameContentDatabase>();
        database.ReplaceEnemyDefinitions(new List<EnemyDefinition> { first, second });

        Dictionary<int, EnemyData> data = database.CreateEnemyDataDictionary();

        Assert.That(data.Count, Is.EqualTo(2));
        Assert.That(data[1].Id, Is.EqualTo("enemy_one"));
        Assert.That(data[2].maxHealth, Is.EqualTo(20));
        Object.DestroyImmediate(database);
        Object.DestroyImmediate(first);
        Object.DestroyImmediate(second);
    }
}
