using System.Globalization;
using IngameDebugConsole;
using UnityEngine;

public static class DebugCommands
{
    [ConsoleMethod("spawn_item", "Spawn item", "item", "position")]
    public static void SpawnItem(string itemName, params string[] positionArgs)
    {
        var database = Resources.Load<ItemsDataBase>("ItemsDatabase");

        if (database == null)
        {
            Debug.LogError("ItemsDatabase not found in Resources.");
            return;
        }

        Item item = database.Get(itemName);

        if (item == null)
        {
            Debug.LogError($"Item '{itemName}' not found.");
            return;
        }

        Vector3 position = ParsePosition(positionArgs);

        Object.Instantiate(item.gameObject, position, Quaternion.identity);

        Debug.Log($"Spawned '{item.name}' at {position}");
    }

    private static Vector3 ParsePosition(string[] args)
    {
        if (args.Length == 0)
            return Vector3.zero;

        switch (args[0].ToLower())
        {
            case "player":
                return ContextManager.Instance.transform.position;

            case "mouse":
            {
                Vector3 pos = InputManager.inputActions.UI.Point.ReadValue<Vector2>();
                pos.z = Mathf.Abs(Camera.main.transform.position.z);
                return Camera.main.ScreenToWorldPoint(pos);
            }

            case "camera":
                return Camera.main.transform.position;
        }

        if (args.Length == 1)
        {
            string[] split = args[0].Split(',');

            if (split.Length == 2)
            {
                return new Vector3(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture),
                    0f);
            }

            if (split.Length == 3)
            {
                return new Vector3(
                    float.Parse(split[0], CultureInfo.InvariantCulture),
                    float.Parse(split[1], CultureInfo.InvariantCulture),
                    float.Parse(split[2], CultureInfo.InvariantCulture));
            }
        }

        if (args.Length == 2)
        {
            return new Vector3(
                float.Parse(args[0], CultureInfo.InvariantCulture),
                float.Parse(args[1], CultureInfo.InvariantCulture),
                0f);
        }

        if (args.Length >= 3)
        {
            return new Vector3(
                float.Parse(args[0], CultureInfo.InvariantCulture),
                float.Parse(args[1], CultureInfo.InvariantCulture),
                float.Parse(args[2], CultureInfo.InvariantCulture));
        }

        return Vector3.zero;
    }
}