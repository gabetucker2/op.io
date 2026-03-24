// Mock example showing how to use the new attributes structs for a new GameObject.
// (All code is commented out for reference only.)
//
// using Microsoft.Xna.Framework;
//
// namespace op.io
// {
//     public static class GameObjectMock
//     {
//         public static GameObject Build()
//         {
//             op.io.Transform.Physics physics = new(
//                 op.io.Transform.PhysicsMotion.Static,
//                 op.io.Transform.CollisionMode.Collidable,
//                 op.io.Transform.DestructionMode.Destructible);
//
//             op.io.Transform.Shape shapeAttributes = new(6);
//
//             Shape renderShape = new(
//                 "Polygon",
//                 width: 64,
//                 height: 64,
//                 sides: shapeAttributes.Sides,
//                 fillColor: Color.CornflowerBlue,
//                 outlineColor: Color.Black,
//                 outlineWidth: 2,
//                 isPrototype: true);
//
//             GameObject obj = new(
//                 id: 999,
//                 name: "Mock Hex",
//                 type: "Prototype",
//                 position: new Vector2(100f, 100f),
//                 rotation: 0f,
//                 mass: 1f,
//                 isDestructible: physics.IsDestructible,
//                 isCollidable: physics.IsCollidable,
//                 staticPhysics: physics.StaticPhysics,
//                 shape: renderShape,
//                 fillColor: Color.CornflowerBlue,
//                 outlineColor: Color.Black,
//                 outlineWidth: 2,
//                 isPrototype: true);
//
//             // Later, you can reapply or change physics flags with the helper.
//             physics.ApplyTo(obj);
//
//             return obj;
//         }
//     }
// }
