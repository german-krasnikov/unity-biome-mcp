using System.Collections;
using System.Text;
using UnityEngine;

public class GridPlayer : MonoBehaviour
{
    public float MoveSpeed = 5f;
    public int GridSize = 10;

    [System.NonSerialized] public int PosX;
    [System.NonSerialized] public int PosZ;
    [System.NonSerialized] public int Score;
    [System.NonSerialized] public bool IsMoving;
    [System.NonSerialized] public int MoveCount;

    void Start()
    {
        Application.runInBackground = true;
        SyncPosition();
    }

    public string Move(string direction)
    {
        if (IsMoving) return "error:already_moving";
        int dx = 0, dz = 0;
        switch (direction?.ToLower())
        {
            case "north": dz = 1; break;
            case "south": dz = -1; break;
            case "east":  dx = 1; break;
            case "west":  dx = -1; break;
            default: return $"error:invalid_direction:{direction}";
        }
        int nx = PosX + dx, nz = PosZ + dz;
        if (nx < 0 || nx >= GridSize || nz < 0 || nz >= GridSize)
            return $"error:out_of_bounds:({nx},{nz})";
        PosX = nx; PosZ = nz; MoveCount++;
        StartCoroutine(AnimateMove(new Vector3(nx, transform.position.y, nz)));
        return "ok";
    }

    public string MoveTo(int x, int z)
    {
        if (IsMoving) return "error:already_moving";
        if (x < 0 || x >= GridSize || z < 0 || z >= GridSize)
            return $"error:out_of_bounds:({x},{z})";
        PosX = x; PosZ = z; MoveCount++;
        StartCoroutine(AnimateMove(new Vector3(x, transform.position.y, z)));
        return "ok";
    }

    public void ResetState()
    {
        StopAllCoroutines();
        PosX = 0; PosZ = 0; Score = 0; MoveCount = 0; IsMoving = false;
        MoveSpeed = 5f;
        SyncPosition();
        foreach (var c in FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            c.gameObject.SetActive(true);
    }

    IEnumerator AnimateMove(Vector3 target)
    {
        IsMoving = true;
        Vector3 start = transform.position;
        float dist = Vector3.Distance(start, target);
        if (dist < 0.01f) { IsMoving = false; CheckPickups(); yield break; }
        float duration = dist / MoveSpeed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Use Time.deltaTime * timeScale for TIMESCALE support
            // but clamp to avoid stuck when Game View not focused
            float dt = Mathf.Max(Time.deltaTime, 0.016f);
            elapsed += dt;
            transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transform.position = target;
        IsMoving = false;
        CheckPickups();
    }

    void CheckPickups()
    {
        foreach (var hit in Physics.OverlapSphere(transform.position, 0.4f))
        {
            var c = hit.GetComponent<Collectible>();
            if (c != null)
            {
                Score++;
                c.gameObject.SetActive(false);
            }
        }
    }

    public string StateText =>
        $"pos={PosX},{PosZ};score={Score};moves={MoveCount};moving={IsMoving};grid={GridSize}";

    public string BoardText()
    {
        var builder = new StringBuilder(GridSize * (GridSize + 1));
        for (var z = GridSize - 1; z >= 0; z--)
        {
            if (z < GridSize - 1)
                builder.Append('/');
            for (var x = 0; x < GridSize; x++)
                builder.Append(CellAt(x, z));
        }
        return builder.ToString();
    }

    char CellAt(int x, int z)
    {
        if (x == PosX && z == PosZ)
            return 'P';
        foreach (var collectible in FindObjectsByType<Collectible>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!collectible.gameObject.activeInHierarchy)
                continue;
            var position = collectible.transform.position;
            if (Mathf.RoundToInt(position.x) == x && Mathf.RoundToInt(position.z) == z)
                return 'C';
        }
        return '.';
    }

    void SyncPosition() => transform.position = new Vector3(PosX, transform.position.y, PosZ);
}
