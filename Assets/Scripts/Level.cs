﻿using UnityEngine;
using System.Collections.Generic;

public class Level : MonoBehaviour {

    public static Level instance { get; private set; }

    const int width = 23;
    const int height = 17;
    public const float SIZE = 2.0f;

    int[,] tiles;
    int[,] paths;
    private Queue<Node> frontier;
    private int pX, pY; // which tile the player is in
    private int pathsGenerated = 0;
    private int greatestCost = 0;
    private int lastX;
    private int lastY;

    public Texture2D[] textures;
    public Object bombPrefab;
    public Object explosionPrefab;
    public bool needToRebuild { private get; set; }

    public Dictionary<int, Bomb> bombs = new Dictionary<int, Bomb>();

    public Transform player;
    Texture2D atlas;
    Rect[] atlasRects;

    public const int GROUND = 0;
    public const int WALL = 1;
    public const int WALL_CRACKED = 2;
    public const int BOMB = 3;

    Mesh mesh;

    List<int> tris = new List<int>();
    List<Vector3> verts = new List<Vector3>();
    List<Vector2> uvs = new List<Vector2>();
    int triNum = 0;


    // Use this for initialization
    void Awake() {
        instance = this;

        atlas = new Texture2D(1024, 1024);
        atlasRects = atlas.PackTextures(textures, 2, 1024);
        atlas.filterMode = FilterMode.Point;
        atlas.wrapMode = TextureWrapMode.Clamp;

        GetComponent<MeshRenderer>().material.mainTexture = atlas;

        Camera.main.transform.position = new Vector3(width / 2.0f, 12.0f, -1.0f) * SIZE;
        Camera.main.transform.rotation = Quaternion.Euler(60.0f, 0.0f, 0.0f);

        GenerateLevel();

        frontier = new Queue<Node>();
        player = GameObject.Find("Player").transform;
    }

    // builds tile array
    public void GenerateLevel() {
        tiles = new int[width, height];
        paths = new int[width, height];

        // generate board
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1 || (x % 2 == 0 && y % 2 == 0)) {
                    tiles[x, y] = WALL;     // if at edge or random chance
                } else if (Random.value < .2f) {
                    tiles[x, y] = WALL_CRACKED;   // random chance
                } else {
                    tiles[x, y] = GROUND;
                }
            }
        }
        BuildMesh();
    }


    // builds mesh from tile data
    public void BuildMesh() {
        if (!mesh) {
            Destroy(mesh);
        }
        verts.Clear();
        tris.Clear();
        uvs.Clear();
        triNum = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int id = tiles[x, y];
                float h = getHeight(x, y) * SIZE;
                float xf = x * SIZE;
                float yf = y * SIZE;

                verts.Add(new Vector3(xf, h, yf));
                verts.Add(new Vector3(xf, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                verts.Add(new Vector3(xf + SIZE, h, yf));

                addUvsAndTris(id, x, y);

                // if height not equal zero check if neighbors are lower to add a wall down that side
                if (h > 0.0f) {
                    if (getHeight(x + 1, y) == 0) { // right neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));

                        addUvsAndTris(id, x + 1, y);
                    }

                    if (getHeight(x - 1, y) == 0) { // left neighbor
                        verts.Add(new Vector3(xf, 0, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf));
                        verts.Add(new Vector3(xf, 0, yf));

                        addUvsAndTris(id, x - 1, y);
                    }

                    if (getHeight(x, y + 1) == 0) { // top neighbor
                        verts.Add(new Vector3(xf + SIZE, 0, yf + SIZE));
                        verts.Add(new Vector3(xf + SIZE, h, yf + SIZE));
                        verts.Add(new Vector3(xf, h, yf + SIZE));
                        verts.Add(new Vector3(xf, 0, yf + SIZE));

                        addUvsAndTris(id, x, y + 1);
                    }

                    if (getHeight(x, y - 1) == 0) { // bottom neighbor
                        verts.Add(new Vector3(xf, 0, yf));
                        verts.Add(new Vector3(xf, h, yf));
                        verts.Add(new Vector3(xf + SIZE, h, yf));
                        verts.Add(new Vector3(xf + SIZE, 0, yf));

                        addUvsAndTris(id, x, y - 1);
                    }
                }
            }
        }

        // build mesh and collider
        mesh = new Mesh();
        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    private void addUvsAndTris(int index, int x, int y) {
        if (index == BOMB) {
            index = GROUND;
        }
        if (index == GROUND && (x + y) % 2 == 0) {
            index = 3;  // hardcoded as the index of the ground_dark texture for now
            // should make a map or something so we could have random wall textures and stuff too
        }

        Rect r = atlasRects[index];

        uvs.Add(new Vector2(r.xMin, r.yMin));
        uvs.Add(new Vector2(r.xMin, r.yMax));
        uvs.Add(new Vector2(r.xMax, r.yMax));
        uvs.Add(new Vector2(r.xMax, r.yMin));

        tris.Add(triNum);
        tris.Add(triNum + 1);
        tris.Add(triNum + 2);
        tris.Add(triNum + 2);
        tris.Add(triNum + 3);
        tris.Add(triNum);

        triNum += 4;
    }

    private int getHeight(int x, int y) {
        if (!insideLevel(x, y)) {
            return 0;
        }
        switch (tiles[x, y]) {
            case WALL:
            case WALL_CRACKED:
                return 1;
            default:
                return 0;
        }
    }

    // safely check for tile id in array
    public int getTile(int x, int y) {
        if (!insideLevel(x, y)) {
            return -1;
        }
        return tiles[x, y];
    }

    // sets tile at x,y to id
    public void setTile(int x, int y, int id) {
        if (!insideLevel(x, y)) {
            return;
        }
        tiles[x, y] = id;
    }

    // if inside level and on a walkable tile
    private bool isWalkable(int x, int y) {
        return insideLevel(x, y) && tiles[x, y] == GROUND;
    }

    // returns whether or not x,y is inside tile array
    private bool insideLevel(int x, int y) {
        return x >= 0 && x < tiles.GetLength(0) && y >= 0 && y < tiles.GetLength(1);
    }

    void Update() {
        if (!player) {
            return;
        }

        pX = (int)(player.position.x / SIZE);
        pY = (int)(player.position.z / SIZE);
        // only generate path if player has changed tile position
        if (pX != lastX || pY != lastY) {
            generatePath(pX, pY);
            pathsGenerated++;
            //Debug.Log(tiles[x][y] + " " + x + " " + y + " " + pathsGenerated);
        }
        lastX = pX;
        lastY = pY;
    }

    public Vector3 getPath(float xPos, float yPos) {
        int x = (int)(xPos / SIZE);
        int y = (int)(yPos / SIZE);

        Vector3 dir = Vector3.zero;
        if (!isWalkable(x, y) || paths[x, y] < 0 || !player) {
            return dir;
        }
        if (x == pX && y == pY) {
            return Vector3.down;
        }

        int shortest = paths[x, y];

        if (Random.value > .5f) {   // random chance to prefer x over y axis and vice versa
            if (isWalkable(x + 1, y) && paths[x + 1, y] < shortest) {
                shortest = paths[x + 1, y];
                dir = getRandomPointInTile(x + 1, y);
            }
            if (isWalkable(x - 1, y) && paths[x - 1, y] < shortest) {
                shortest = paths[x - 1, y];
                dir = getRandomPointInTile(x - 1, y);
            }
            if (isWalkable(x, y + 1) && paths[x, y + 1] < shortest) {
                shortest = paths[x, y + 1];
                dir = getRandomPointInTile(x, y + 1);
            }
            if (isWalkable(x, y - 1) && paths[x, y - 1] < shortest) {
                shortest = paths[x, y - 1];
                dir = getRandomPointInTile(x, y - 1);
            }
        } else {
            if (isWalkable(x, y + 1) && paths[x, y + 1] < shortest) {
                shortest = paths[x, y + 1];
                dir = getRandomPointInTile(x, y + 1);
            }
            if (isWalkable(x, y - 1) && paths[x, y - 1] < shortest) {
                shortest = paths[x, y - 1];
                dir = getRandomPointInTile(x, y - 1);
            }
            if (isWalkable(x + 1, y) && paths[x + 1, y] < shortest) {
                shortest = paths[x + 1, y];
                dir = getRandomPointInTile(x + 1, y);
            }
            if (isWalkable(x - 1, y) && paths[x - 1, y] < shortest) {
                shortest = paths[x - 1, y];
                dir = getRandomPointInTile(x - 1, y);
            }
        }
        dir -= new Vector3(xPos, 0f, yPos);
        return dir.normalized;
    }

    private Vector3 getRandomPointInTile(int x, int y) {
        if (!insideLevel(x, y)) {
            return new Vector3(x * SIZE, 0f, y * SIZE);
        }
        return new Vector3(x * SIZE + Random.value * SIZE, 0f, y * SIZE + Random.value * SIZE);
    }

    private void generatePath(int x, int y) {
        // clear path
        for (int i = 0; i < paths.GetLength(0); i++) {
            for (int j = 0; j < paths.GetLength(1); j++) {
                paths[i, j] = -1;
            }
        }

        frontier.Clear();
        frontier.Enqueue(new Node(x, y));
        paths[x, y] = 0;
        while (frontier.Count > 0) {
            Node n = frontier.Dequeue();
            greatestCost = Mathf.Max(greatestCost, paths[n.x, n.y]);
            // right neigbor
            if (isWalkable(n.x + 1, n.y) && paths[n.x + 1, n.y] < 0) {
                frontier.Enqueue(new Node(n.x + 1, n.y));
                paths[n.x + 1, n.y] = paths[n.x, n.y] + 1;
            }
            // left neighbor
            if (isWalkable(n.x - 1, n.y) && paths[n.x - 1, n.y] < 0) {
                frontier.Enqueue(new Node(n.x - 1, n.y));
                paths[n.x - 1, n.y] = paths[n.x, n.y] + 1;
            }
            // front neighbor
            if (isWalkable(n.x, n.y + 1) && paths[n.x, n.y + 1] < 0) {
                frontier.Enqueue(new Node(n.x, n.y + 1));
                paths[n.x, n.y + 1] = paths[n.x, n.y] + 1;
            }
            // back neighbor
            if (isWalkable(n.x, n.y - 1) && paths[n.x, n.y - 1] < 0) {
                frontier.Enqueue(new Node(n.x, n.y - 1));
                paths[n.x, n.y - 1] = paths[n.x, n.y] + 1;
            }
        }
    }


    private struct Node {
        public int x;
        public int y;

        public Node(int x, int y) {
            this.x = x;
            this.y = y;
        }
    }

    // returns 1d tile position in array based on pos
    public int getTilePos(Vector3 pos) {
        return (int)(pos.z / SIZE) * width + (int)(pos.x / SIZE);
    }

    // figure out which tile 'pos' is in
    // then place bomb prefab there
    public void placeBomb(Vector3 pos) {
        int x = (int)(pos.x / SIZE);
        int y = (int)(pos.z / SIZE);

        if (getTile(x, y) != GROUND) {   // if not on ground or outside of tile array then return
            return;
        }
        tiles[x, y] = BOMB;

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, 0.0f, yf);

        GameObject go = (GameObject)Instantiate(bombPrefab, spawn, Quaternion.identity);
        go.name = "Bomb";
        Bomb b = go.GetComponent<Bomb>();
        b.init(x, y, this);
        bombs.Add(y * width + x, b);
    }

    public void spawnExplosion(int x, int y, int dx, int dy, int life) {
        int id = getTile(x, y);
        if (id == WALL) {    // this explosion hit a wall
            return;
        }
        setTile(x, y, GROUND);
        if (id == WALL_CRACKED) {
            needToRebuild = true;
            life = 0; // reduce life of explosion to zero so it wont spread anymore
        }
        if (id == BOMB) {    // this explosion hit a bomb so blow bomb up now
            bombs[y * width + x].explode();
            bombs.Remove(y * width + x);
            return;
        }

        float xf = x * SIZE + SIZE * 0.5f;
        float yf = y * SIZE + SIZE * 0.5f;
        Vector3 spawn = new Vector3(xf, SIZE * 0.5f, yf);
        GameObject go = (GameObject)Instantiate(explosionPrefab, spawn, Quaternion.identity);
        go.name = "Explosion";
        go.GetComponent<Explosion>().start(x, y, dx, dy, life, this);
    }

    public Vector3 getRandomGroundPosition() {
        List<int> spots = new List<int>();
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (getTile(x, y) == GROUND) {
                    spots.Add(y * width + x);
                }
            }
        }
        int r = spots[Random.Range(0, spots.Count)];
        return new Vector3(r % width, 0.1f, r / width) * SIZE + Vector3.one * SIZE * 0.5f;
    }

    void LateUpdate() {
        if (needToRebuild) {
            BuildMesh();
            needToRebuild = false;
        }
    }

    // to visualize path distance
    void OnDrawGizmos() {
        bool drawPathData = true;
        if (paths == null || !drawPathData) {
            return;
        }
        for (int x = 0; x < paths.GetLength(0); x++) {
            for (int y = 0; y < paths.GetLength(1); y++) {
                float c = paths[x, y];
                if (c >= 0) {
                    Gizmos.color = new Color(1f - c / greatestCost, 0f, c / greatestCost);
                    if (c == 0) {
                        Gizmos.color = Color.yellow;
                    }
                    float maxH = 5f;
                    float height = maxH - c / greatestCost * maxH;
                    Gizmos.DrawCube(new Vector3((x + .5f) * SIZE, height / 2f, (y + .5f) * SIZE), new Vector3(.5f, height, .5f));
                }
            }
        }
    }

}
