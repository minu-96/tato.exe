using System.Collections.Generic;
using UnityEngine;

public class CellSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Cell applePrefab;
    [SerializeField] private int rowCount = 5;
    [SerializeField] private int columnCount = 5;
    [SerializeField] private float spacing = 1.3f;
    [SerializeField] private Transform startPoint;

    private readonly List<Cell> spawnedApples = new List<Cell>();

    public void SpawnBoard()
    {
        ClearBoard();

        for (int row = 0; row < rowCount; row++)
        {
            for (int col = 0; col < columnCount; col++)
            {
                Vector3 spawnPosition = startPoint.position + new Vector3(col * spacing, -row * spacing, 0f);
                CreateApple(spawnPosition);
            }
        }
    }

    private void CreateApple(Vector3 position)
    {
        Cell newApple = Instantiate(applePrefab, position, Quaternion.identity, transform);

        int randomValue = Random.Range(1, 10);
        newApple.Initialize(randomValue);

        spawnedApples.Add(newApple);
    }

    public List<Cell> GetAllApples()
    {
        spawnedApples.RemoveAll(apple => apple == null);
        return spawnedApples;
    }

    public void RemoveAppleFromList(Cell apple)
    {
        if (spawnedApples.Contains(apple))
        {
            spawnedApples.Remove(apple);
        }
    }

    public void ClearBoard()
    {
        for (int i = spawnedApples.Count - 1; i >= 0; i--)
        {
            if (spawnedApples[i] != null)
            {
                Destroy(spawnedApples[i].gameObject);
            }
        }

        spawnedApples.Clear();
    }
}