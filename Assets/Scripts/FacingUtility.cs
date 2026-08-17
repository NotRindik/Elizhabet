using UnityEngine;

// Без namespace — доступен из любого файла проекта без доп. using.
public static class FacingUtility
{
    // Текущее направление взгляда как знак: 1 = вправо (дефолт), -1 = влево (флип).
    // Работает по transform.right, а не по eulerAngles.y напрямую, чтобы не ловить
    // пограничные случаи представления угла (0 vs 360 и т.п.).
    public static float FacingSign(this Transform t) => t.right.x < 0f ? -1f : 1f;

    public static bool IsFacingLeft(this Transform t) => t.right.x < 0f;

    // Ставит флип по Y (0 или 180), СОХРАНЯЯ текущие X/Z повороты этого transform.
    // Именно эту сохранность нужно соблюдать — иначе любой другой код, который
    // крутит объект по Z (лазание по стенам, прицеливание и т.п.), будет случайно
    // сбрасываться при каждом вызове SetFacing.
    public static void SetFacing(this Transform t, float sign)
    {
        Vector3 e = t.eulerAngles;
        t.rotation = Quaternion.Euler(e.x, sign < 0f ? 180f : 0f, e.z);
    }

    // Обратная пара: ставит Z-поворот (углы прицеливания/лазания/наклона),
    // СОХРАНЯЯ текущий Y (флип). Использовать вместо прямого
    // "transform.rotation = Quaternion.Euler(0, 0, z)" везде, где объект
    // одновременно и флипается через SpriteFlipSystem, и крутится сам по Z.
    public static void SetZRotation(this Transform t, float zDegrees)
    {
        t.rotation = Quaternion.Euler(0f, t.eulerAngles.y, zDegrees);
    }
}
