using Controllers;
using System;
using System.Collections;
using System.Collections.Generic;
using Systems;
using UnityEngine;

public class ExtraCheckPoint : MonoBehaviour
{
    public void SetActiveCheckPoint() => ContextManager.Instance.extraSpawnManager.SetActiveCheckPoint(this);
    private void Start()
    {
        ContextManager.Instance.extraSpawnManager.AddPoint(this);   
    }
}


public class ExtraSpawnManager : IDisposable
{
    public List<ExtraCheckPoint> points = new();
    public ExtraCheckPoint currPoint;

    public PlayerController Player => ContextManager.Instance.player;

    public bool isRespawning;

    public void Dispose()
    {
        points = null;
    }

    public void SetActiveCheckPoint(ExtraCheckPoint point)
    {
        currPoint = point;  
    }


    public void AddPoint(ExtraCheckPoint point) => points.Add(point);


    public void StartRespawn(DamageComponent dmg)
    {
        if(!isRespawning) 
            App.Instance.StartCoroutine(RespawnProcess(dmg));
    }

    public IEnumerator RespawnProcess(DamageComponent dmg)
    {
        isRespawning = true;
        var hp = Player.GetControllerSystem<HealthSystem>();
        hp.IsActive = false;
        
        yield return TransitionEffect.Instance.BlendInCoroutine(0.3f);
        ExtraCheckPoint point = currPoint;
        point ??= FindPoint();
        
        var pointPos = point.transform.position;
        pointPos.z = Player.transform.position.z;
        
        yield return new WaitForSeconds(0.6f);
        Player.transform.position = pointPos;
        
        hp.IsActive = true;
        var hit = new HitInfo(){Target = Player};
        new Damage(dmg).ApplyDamage(hp,ref hit);

        yield return TransitionEffect.Instance.BlendOutCoroutine(1f);

        isRespawning = false;
    }

    public ExtraCheckPoint FindPoint()
    {
        if (points == null || points.Count == 0)
            return null;

        Vector3 playerPos = Player.transform.position;

        ExtraCheckPoint closest = null;
        float closestDist = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            ExtraCheckPoint point = points[i];

            float dist = (point.transform.position - playerPos).sqrMagnitude;

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = point;
            }
        }

        return closest;
    }
}