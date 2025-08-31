using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

[assembly: CommandClass(typeof(FloorPlanProcessor.Commands))]

namespace FloorPlanProcessor
{
    public class Commands
    {
        private ProcessingSettings _settings;

        private void LoadSettings()
        {
            var settingsPath = Path.Combine(Environment.CurrentDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                _settings = JsonSerializer.Deserialize<ProcessingSettings>(json);
            }
            else
            {
                _settings = new ProcessingSettings
                {
                    BoxDistribution = new List<BoxSizeDistribution>
                    {
                        new BoxSizeDistribution { Percentage = 60, MinArea = 10, MaxArea = 50 }
                    },
                    CorridorWidth = 1200
                };
            }
        }

        [CommandMethod("PROCESS_FLOOR_PLAN")]
        public void ProcessFloorPlan()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                LoadSettings();

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    ed.WriteMessage("\n=== Floor Plan Processing Started ===");

                    // 1. Identify zones
                    var walls = IdentifyEntitiesByColor(trans, db, 0, 0, 0); // Black
                    var forbiddenZones = IdentifyEntitiesByColor(trans, db, 0, 0, 255); // Blue
                    var entrances = IdentifyEntitiesByColor(trans, db, 255, 0, 0); // Red

                    // 2. Generate and place boxes
                    var totalArea = 1000.0; // Calculate from drawing bounds
                    var ilots = GenerateIlotsFromDistribution(totalArea);
                    PlaceIlotsWithConstraints(trans, db, ilots, walls, forbiddenZones, entrances);
                    SaveDrawing(db, "ilots_plan.dwg");

                    // 3. Generate corridors
                    var corridors = GenerateCorridors(trans, db, ilots);
                    DrawCorridors(trans, db, corridors);
                    
                    SaveDrawing(db, "final_plan.dwg");
                    ExportToPng(db, "final_plan.png");
                    
                    trans.Commit();
                    
                    ed.WriteMessage("\n=== Processing Complete ===");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nError: {ex.Message}");
            }
        }

        private List<ObjectId> IdentifyEntitiesByColor(Transaction trans, Database db, byte r, byte g, byte b)
        {
            var objectIds = new List<ObjectId>();
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId objId in btr)
            {
                var entity = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity != null && entity.Color.Red == r && entity.Color.Green == g && entity.Color.Blue == b)
                {
                    objectIds.Add(objId);
                }
            }
            return objectIds;
        }

        private List<Ilot> GenerateIlotsFromDistribution(double totalArea)
        {
            var ilots = new List<Ilot>();
            var rand = new Random();

            foreach (var dist in _settings.BoxDistribution)
            {
                var areaForThisCategory = totalArea * (dist.Percentage / 100.0);
                var averageArea = (dist.MinArea + dist.MaxArea) / 2.0;
                if (averageArea == 0) continue;

                var numIlots = (int)Math.Floor(areaForThisCategory / averageArea);

                for (int i = 0; i < numIlots; i++)
                {
                    var area = dist.MinArea + rand.NextDouble() * (dist.MaxArea - dist.MinArea);
                    ilots.Add(new Ilot { Area = area });
                }
            }
            return ilots;
        }

        private void PlaceIlotsWithConstraints(Transaction trans, Database db, List<Ilot> ilots, List<ObjectId> walls, List<ObjectId> forbiddenZones, List<ObjectId> entrances)
        {
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var placementGrid = new bool[100, 100]; // Simplified grid for collision detection

            foreach (var ilot in ilots.OrderByDescending(i => i.Area))
            {
                bool placed = false;
                for (int i = 0; i < 100 && !placed; i++)
                {
                    for (int j = 0; j < 100 && !placed; j++)
                    {
                        if (!placementGrid[i, j])
                        {
                            var size = Math.Sqrt(ilot.Area) * 1000;
                            var x = i * 1000;
                            var y = j * 1000;
                            
                            var rect = new Polyline();
                            rect.AddVertexAt(0, new Point2d(x, y), 0, 0, 0);
                            rect.AddVertexAt(1, new Point2d(x + size, y), 0, 0, 0);
                            rect.AddVertexAt(2, new Point2d(x + size, y + size), 0, 0, 0);
                            rect.AddVertexAt(3, new Point2d(x, y + size), 0, 0, 0);
                            rect.Closed = true;
                            rect.Color = Autodesk.AutoCAD.Colors.Color.FromRgb(0, 255, 0);

                            // Simple collision check
                            if (!CheckCollision(rect, forbiddenZones, entrances))
                            {
                                btr.AppendEntity(rect);
                                trans.AddNewlyCreatedDBObject(rect, true);
                                placementGrid[i, j] = true;
                                placed = true;
                            }
                        }
                    }
                }
            }
        }

        private bool CheckCollision(Polyline rect, List<ObjectId> forbidden, List<ObjectId> entrances)
        {
            // In a real application, this would involve complex geometric intersection tests.
            // For this example, we'll keep it simple.
            return false;
        }
        
        private List<Corridor> GenerateCorridors(Transaction trans, Database db, List<Ilot> ilots)
        {
            var corridors = new List<Corridor>();
            var ilotGroups = GroupIlotsIntoRows(ilots);

            foreach (var group in ilotGroups)
            {
                for (int i = 0; i < group.Count - 1; i++)
                {
                    var ilot1 = group[i];
                    var ilot2 = group[i + 1];

                    // Check if ilots are facing each other
                    if (AreIlotsFacing(ilot1, ilot2))
                    {
                        var corridor = new Corridor
                        {
                            StartPoint = GetMidpoint(ilot1.Boundary),
                            EndPoint = GetMidpoint(ilot2.Boundary),
                            Width = _settings.CorridorWidth
                        };
                        corridors.Add(corridor);
                    }
                }
            }

            return corridors;
        }

        private List<List<Ilot>> GroupIlotsIntoRows(List<Ilot> ilots)
        {
            // Simplified grouping logic
            return ilots.GroupBy(i => (int)(i.Boundary.StartPoint.Y / 10000))
                        .Select(g => g.ToList())
                        .ToList();
        }

        private bool AreIlotsFacing(Ilot ilot1, Ilot ilot2)
        {
            // Simplified check
            return Math.Abs(ilot1.Boundary.StartPoint.X - ilot2.Boundary.StartPoint.X) < 5000;
        }

        private Point3d GetMidpoint(Polyline poly)
        {
            return new Point3d(
                (poly.GetPoint3dAt(0).X + poly.GetPoint3dAt(2).X) / 2,
                (poly.GetPoint3dAt(0).Y + poly.GetPoint3dAt(2).Y) / 2,
                0);
        }

        private void CreateEmptyPlan(Database db, Transaction trans)
        {
            // Clean drawing
            CleanDrawing(db, trans);
            
            // Keep only structural elements
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            
            var entitiesToKeep = new List<ObjectId>();
            
            foreach (ObjectId objId in btr)
            {
                var entity = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                if (entity != null && IsStructuralElement(entity))
                {
                    entitiesToKeep.Add(objId);
                }
            }
            
            // Remove non-structural elements
            var allEntities = new List<ObjectId>();
            foreach (ObjectId objId in btr)
            {
                allEntities.Add(objId);
            }
            
            foreach (var objId in allEntities)
            {
                if (!entitiesToKeep.Contains(objId))
                {
                    var entity = trans.GetObject(objId, OpenMode.ForWrite);
                    entity.Erase();
                }
            }
        }

        private bool IsStructuralElement(Entity entity)
        {
            if (entity == null) return false;
            
            var layer = entity.Layer.ToUpper();
            
            // Wall layers
            if (layer.Contains("WALL") || layer.Contains("MUR") || layer.Contains("CLOISON"))
                return true;
            
            // Door layers
            if (layer.Contains("DOOR") || layer.Contains("PORTE") || layer.Contains("ENTREE"))
                return true;
            
            // Window layers
            if (layer.Contains("WINDOW") || layer.Contains("FENETRE"))
                return true;
            
            // Check geometry characteristics for walls
            if (entity is Polyline poly && poly.ConstantWidth >= 50) // 5cm+ walls
                return true;
            
            if (entity is Line line)
            {
                // Thick lines likely walls
                if (entity.LineWeight == LineWeight.LineWeight070 || entity.LineWeight == LineWeight.LineWeight100)
                    return true;
            }
            
            return false;
        }

        private List<Room> DetectRooms(Database db, Transaction trans)
        {
            var rooms = new List<Room>();
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            
            // Find closed polylines that represent rooms
            foreach (ObjectId objId in btr)
            {
                var entity = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                
                if (entity is Polyline poly && poly.Closed)
                {
                    var area = poly.Area / 1000000; // Convert mm² to m²
                    
                    if (area > 5) // Minimum 5m² for a room
                    {
                        var room = new Room
                        {
                            Boundary = poly,
                            Area = area,
                            IsNoEntryZone = IsNoEntryZone(entity.Layer),
                            Center = GetPolygonCenter(poly)
                        };
                        rooms.Add(room);
                    }
                }
            }
            
            return rooms;
        }

        private bool IsNoEntryZone(string layer)
        {
            var layerUpper = layer.ToUpper();
            return _settings.NoEntryLayers.Any(nel => layerUpper.Contains(nel.ToUpper()));
        }

        private Point3d GetPolygonCenter(Polyline poly)
        {
            double totalX = 0, totalY = 0;
            int count = 0;
            
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                var vertex = poly.GetPoint2dAt(i);
                totalX += vertex.X;
                totalY += vertex.Y;
                count++;
            }
            
            return new Point3d(totalX / count, totalY / count, 0);
        }

        private List<IlotPlacement> CalculateIlotPlacements(List<Room> rooms)
        {
            var placements = new List<IlotPlacement>();
            
            foreach (var room in rooms.Where(r => !r.IsNoEntryZone))
            {
                var ilotSize = CalculateOptimalIlotSize(room);
                if (ilotSize.Width > 0 && ilotSize.Height > 0)
                {
                    var placement = new IlotPlacement
                    {
                        Position = CalculateOptimalPosition(room, ilotSize),
                        Size = ilotSize,
                        RoomId = rooms.IndexOf(room)
                    };
                    placements.Add(placement);
                }
            }
            
            return placements;
        }

        private Size2d CalculateOptimalIlotSize(Room room)
        {
            // Calculate based on room area and constraints
            var maxDimension = Math.Min(_settings.MaxIlotSize, Math.Sqrt(room.Area * 1000000) * 0.6);
            var minDimension = Math.Max(_settings.MinIlotSize, Math.Sqrt(room.Area * 1000000) * 0.3);
            
            if (maxDimension < _settings.MinIlotSize)
                return new Size2d(0, 0); // Room too small
            
            // Rectangular ilot - adjust based on room shape
            var width = Math.Min(maxDimension, _settings.MaxIlotSize);
            var height = Math.Min(maxDimension * 0.7, _settings.MaxIlotSize * 0.7);
            
            return new Size2d(width, height);
        }

        private Point3d CalculateOptimalPosition(Room room, Size2d ilotSize)
        {
            var center = room.Center;
            var clearance = _settings.IlotClearance;
            
            // Position with clearance from walls
            var x = center.X - ilotSize.Width / 2;
            var y = center.Y - ilotSize.Height / 2;
            
            return new Point3d(x, y, 0);
        }

        private void PlaceIlots(Database db, Transaction trans, List<IlotPlacement> ilots)
        {
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            
            CreateLayer(trans, db, "ILOT", 3); // Green color
            
            foreach (var ilot in ilots)
            {
                var rect = new Polyline();
                rect.AddVertexAt(0, new Point2d(ilot.Position.X, ilot.Position.Y), 0, 0, 0);
                rect.AddVertexAt(1, new Point2d(ilot.Position.X + ilot.Size.Width, ilot.Position.Y), 0, 0, 0);
                rect.AddVertexAt(2, new Point2d(ilot.Position.X + ilot.Size.Width, ilot.Position.Y + ilot.Size.Height), 0, 0, 0);
                rect.AddVertexAt(3, new Point2d(ilot.Position.X, ilot.Position.Y + ilot.Size.Height), 0, 0, 0);
                rect.Closed = true;
                rect.Layer = "ILOT";
                
                // Add hatch pattern
                var hatch = new Hatch();
                hatch.Layer = "ILOT";
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                hatch.ColorIndex = 3;
                
                btr.AppendEntity(rect);
                btr.AppendEntity(hatch);
                trans.AddNewlyCreatedDBObject(rect, true);
                trans.AddNewlyCreatedDBObject(hatch, true);
                
                var objIds = new ObjectIdCollection();
                objIds.Add(rect.ObjectId);
                hatch.AppendLoop(HatchLoopTypes.Outermost, objIds);
                hatch.EvaluateHatch(true);
            }
        }

        private List<Corridor> GenerateCorridors(Database db, Transaction trans, List<Room> rooms, List<IlotPlacement> ilots)
        {
            var corridors = new List<Corridor>();
            var corridorWidth = _settings.CorridorWidth;
            
            // Find main circulation paths between rooms
            var connectionPoints = FindRoomConnections(rooms);
            
            foreach (var connection in connectionPoints)
            {
                var corridor = new Corridor
                {
                    StartPoint = connection.Start,
                    EndPoint = connection.End,
                    Width = corridorWidth,
                    CenterLine = GenerateCenterLine(connection.Start, connection.End)
                };
                corridors.Add(corridor);
            }
            
            return corridors;
        }

        private List<RoomConnection> FindRoomConnections(List<Room> rooms)
        {
            var connections = new List<RoomConnection>();
            
            // Simple implementation - connect adjacent rooms
            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (AreRoomsAdjacent(rooms[i], rooms[j]))
                    {
                        connections.Add(new RoomConnection
                        {
                            Start = rooms[i].Center,
                            End = rooms[j].Center
                        });
                    }
                }
            }
            
            return connections;
        }

        private bool AreRoomsAdjacent(Room room1, Room room2)
        {
            var distance = room1.Center.DistanceTo(room2.Center);
            return distance < 5000; // 5m threshold
        }

        private List<Point3d> GenerateCenterLine(Point3d start, Point3d end)
        {
            return new List<Point3d> { start, end }; // Simplified - straight line
        }

        private void DrawCorridors(Transaction trans, Database db, List<Corridor> corridors)
        {
            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            
            CreateLayer(trans, db, "CORRIDOR", 5); // Blue color

            foreach (var corridor in corridors)
            {
                var corridorLine = new Polyline();
                corridorLine.AddVertexAt(0, new Point2d(corridor.StartPoint.X, corridor.StartPoint.Y), 0, 0, 0);
                corridorLine.AddVertexAt(1, new Point2d(corridor.EndPoint.X, corridor.EndPoint.Y), 0, 0, 0);
                corridorLine.ConstantWidth = corridor.Width;
                corridorLine.Layer = "CORRIDOR";
                
                btr.AppendEntity(corridorLine);
                trans.AddNewlyCreatedDBObject(corridorLine, true);
            }
        }

        private void ExportToPng(Database db, string fileName)
        {
            // In a real application, you would use a library like ImageMagick or a dedicated
            // rendering service to convert the DWG to a high-resolution PNG.
            // For this example, we'll create a dummy file.
            File.Create(fileName).Close();
        }

        private void CreateLayer(Transaction trans, Database db, string layerName, short colorIndex)
        {
            var layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForWrite);
            
            if (!layerTable.Has(layerName))
            {
                var layerRecord = new LayerTableRecord();
                layerRecord.Name = layerName;
                layerRecord.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
                
                layerTable.Add(layerRecord);
                trans.AddNewlyCreatedDBObject(layerRecord, true);
            }
        }

        private void CleanDrawing(Database db, Transaction trans)
        {
            // Purge unused objects
            var purgeIds = new ObjectIdCollection();
            db.Purge(purgeIds);
            if (purgeIds.Count > 0)
            {
                foreach (ObjectId id in purgeIds)
                {
                    var obj = trans.GetObject(id, OpenMode.ForWrite);
                    obj.Erase();
                }
            }
            
            // Set units
            db.Insunits = UnitsValue.Millimeters;
        }

        private void SaveDrawing(Database db, string fileName)
        {
            var filePath = Path.Combine(Environment.CurrentDirectory, fileName);
            db.SaveAs(filePath, DwgVersion.Current);
        }

        private MeasurementData CalculateMeasurements(Database db, Transaction trans, List<Room> rooms, List<IlotPlacement> ilots, List<Corridor> corridors)
        {
            var totalArea = rooms.Sum(r => r.Area);
            var ilotArea = ilots.Sum(i => i.Size.Width * i.Size.Height / 1000000); // Convert to m²
            var corridorArea = corridors.Sum(c => c.Width * c.StartPoint.DistanceTo(c.EndPoint) / 1000000);
            var corridorLength = corridors.Sum(c => c.StartPoint.DistanceTo(c.EndPoint) / 1000); // Convert to m
            
            return new MeasurementData
            {
                TotalArea = totalArea,
                WalkableArea = totalArea - ilotArea,
                IlotArea = ilotArea,
                CorridorArea = corridorArea,
                NumberOfIlots = ilots.Count,
                CorridorLength = corridorLength
            };
        }

        private void SaveMeasurements(MeasurementData measurements)
        {
            var json = JsonSerializer.Serialize(measurements, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory, "measurements.json"), json);
        }
    }

    // Supporting classes
    public class ProcessingSettings
    {
        public List<BoxSizeDistribution> BoxDistribution { get; set; }
        public double CorridorWidth { get; set; } = 1200;
        public List<string> NoEntryLayers { get; set; } = new List<string> { "FORBIDDEN", "NO_ENTRY" };
        public double MaxIlotSize { get; set; } = 5000;
        public double MinIlotSize { get; set; } = 1000;
        public double IlotClearance { get; set; } = 800;
    }

    public class BoxSizeDistribution
    {
        public double Percentage { get; set; }
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
    }

    public class Room
    {
        public Polyline Boundary { get; set; }
        public double Area { get; set; }
        public bool IsNoEntryZone { get; set; }
        public Point3d Center { get; set; }
    }

    public class IlotPlacement
    {
        public Point3d Position { get; set; }
        public Size2d Size { get; set; }
        public int RoomId { get; set; }
    }

    public class Ilot
    {
        public double Area { get; set; }
        public Polyline Boundary { get; set; }
    }

    public class Corridor
    {
        public Point3d StartPoint { get; set; }
        public Point3d EndPoint { get; set; }
        public double Width { get; set; }
        public List<Point3d> CenterLine { get; set; }
    }

    public class RoomConnection
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
    }

    public class Size2d
    {
        public double Width { get; set; }
        public double Height { get; set; }
        
        public Size2d(double width, double height)
        {
            Width = width;
            Height = height;
        }
    }

    public class MeasurementData
    {
        public double TotalArea { get; set; }
        public double WalkableArea { get; set; }
        public double IlotArea { get; set; }
        public double CorridorArea { get; set; }
        public int NumberOfIlots { get; set; }
        public double CorridorLength { get; set; }
    }
}