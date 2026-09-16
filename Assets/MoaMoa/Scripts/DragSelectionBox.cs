using System.Collections.Generic;
using UnityEngine;

public class DragSelectionBox : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RectTransform selectionBox;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CellSpawner appleSpawner;
    [SerializeField] private GameManager gameManager;

    private readonly List<Cell> currentSelectedApples = new List<Cell>();

    private Vector2 dragStartScreenPosition;
    private bool isDragging;
    private RectTransform canvasRect;

    private void Awake()
    {
        canvasRect = canvas.GetComponent<RectTransform>();

        if (selectionBox != null)
        {
            selectionBox.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!gameManager.CanInput())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            BeginDrag();
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            UpdateDrag();
        }

        if (isDragging && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    private void BeginDrag()
    {
        isDragging = true;
        dragStartScreenPosition = Input.mousePosition;

        currentSelectedApples.Clear();

        if (selectionBox != null)
        {
            selectionBox.gameObject.SetActive(true);
            UpdateSelectionBoxVisual(dragStartScreenPosition, dragStartScreenPosition);
        }
    }

    private void UpdateDrag()
    {
        Vector2 currentScreenPosition = Input.mousePosition;

        UpdateSelectionBoxVisual(dragStartScreenPosition, currentScreenPosition);
        RefreshSelectedApples(dragStartScreenPosition, currentScreenPosition);
    }

    private void EndDrag()
    {
        isDragging = false;

        if (selectionBox != null)
        {
            selectionBox.gameObject.SetActive(false);
        }

        gameManager.EvaluateSelection(new List<Cell>(currentSelectedApples));
        ClearSelection();
    }

private void RefreshSelectedApples(Vector2 startScreen, Vector2 endScreen)
{
    foreach (Cell apple in currentSelectedApples)
        if (apple != null) apple.SetSelected(false);

    currentSelectedApples.Clear();

    Vector2 min = Vector2.Min(startScreen, endScreen);
    Vector2 max = Vector2.Max(startScreen, endScreen);

    List<Cell> allApples = appleSpawner.GetAllApples();

    foreach (Cell apple in allApples)
    {
        if (apple == null) continue;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(apple.transform.position);
        if (screenPos.x >= min.x && screenPos.x <= max.x &&
            screenPos.y >= min.y && screenPos.y <= max.y)
        {
            currentSelectedApples.Add(apple);
            apple.SetSelected(true);
        }
    }

    // ★ 합이 10이면 독감자색으로 덮어쓰기
    int sum = 0;
    foreach (var p in currentSelectedApples)
        if (p != null) sum += p.value;

    if (sum == 10)
    {
        foreach (var p in currentSelectedApples)
            if (p != null) p.SetPoison();
    }
}

    private void UpdateSelectionBoxVisual(Vector2 startScreen, Vector2 endScreen)
    {
        Vector2 startLocal;
        Vector2 endLocal;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            startScreen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out startLocal
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            endScreen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out endLocal
        );

        Vector2 center = (startLocal + endLocal) * 0.5f;
        Vector2 size = new Vector2(
            Mathf.Abs(endLocal.x - startLocal.x),
            Mathf.Abs(endLocal.y - startLocal.y)
        );

        selectionBox.anchoredPosition = center;
        selectionBox.sizeDelta = size;
    }

    public void ClearSelection()
    {
        foreach (Cell apple in currentSelectedApples)
        {
            if (apple != null)
            {
                apple.SetSelected(false);
            }
        }

        currentSelectedApples.Clear();
    }
}