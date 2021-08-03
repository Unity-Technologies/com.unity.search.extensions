using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.Search
{
    internal class Point
    {
        public Point(Vector2 iVelocity, Vector2 iAcceleration, Node iNode)
        {
            node = iNode;
            velocity = iVelocity;
            acceleration = iAcceleration;
        }

        public override int GetHashCode()
        {
            return position.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            // If parameter cannot be cast to Point return false.
            Point p = obj as Point;
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return position == p.position;
        }

        public bool Equals(Point p)
        {
            // If parameter is null return false:
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return position == p.position;
        }

        public static bool operator==(Point a, Point b)
        {
            // If both are null, or both are same instance, return true.
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if ((object)a == null || (object)b == null)
            {
                return false;
            }

            // Return true if the fields match:
            return a.position == b.position;
        }

        public static bool operator!=(Point a, Point b)
        {
            return !(a == b);
        }

        public void ApplyForce(Vector2 force)
        {
            acceleration = acceleration + force / mass;
        }

        public Vector2 position
        {
            get { return node.rect.center; }
            set
            {
                if (node == null)
                    return;
                node.rect.center = value;
            }
        }

        public float mass { get { return node.mass; } }

        public Node node { get; set; }
        public Vector2 velocity { get; set; }
        public Vector2 acceleration { get; set; }
    }

    internal class Spring
    {
        public Spring(Point iPoint1, Point iPoint2, float iLength, float iK)
        {
            point1 = iPoint1;
            point2 = iPoint2;
            Length = iLength;
            K = iK;
        }

        public Point point1;
        public Point point2;
        public float Length;
        public float K;
    }

    internal class ForceDirectedLayout : IGraphLayout
    {
        public float Stiffness;
        public float Repulsion;
        public float Damping;
        public float Threadshold;
        public bool WithinThreashold;
        public int FixedIterations;

        protected Dictionary<string, Point> m_NodePoints;
        protected Dictionary<string, Spring> m_EdgeSprings;

        public Graph graph { get; protected set; }

        public bool Animated { get { return true; } }

        public delegate void EdgeAction(Edge e, Spring s);
        public delegate void NodeAction(Node n, Point p);

        public ForceDirectedLayout(Graph iGraph)
        {
            graph = iGraph;
            Stiffness = 161.76f;
            Repulsion = 80000.0f;
            Damping = 0.945f;
            m_NodePoints = new Dictionary<string, Point>();
            m_EdgeSprings = new Dictionary<string, Spring>();
            FixedIterations = -1;
            Threadshold = 0.01f;
        }

        public void Clear()
        {
            m_NodePoints.Clear();
            m_EdgeSprings.Clear();
            graph.Clear();
        }

        public Spring GetSpring(Edge iEdge)
        {
            if (!m_EdgeSprings.ContainsKey(iEdge.ID))
            {
                float length = iEdge.length;
                Spring existingSpring = null;

                var fromEdge = graph.GetEdgeBetweenNodes(iEdge.Source, iEdge.Target);
                if (fromEdge != null)
                {
                    if (m_EdgeSprings.ContainsKey(fromEdge.ID))
                        existingSpring = m_EdgeSprings[fromEdge.ID];
                }
                if (existingSpring != null)
                    return new Spring(existingSpring.point1, existingSpring.point2, 0.0f, 0.0f);

                var toEdge = graph.GetEdgeBetweenNodes(iEdge.Target, iEdge.Source);
                if (toEdge != null)
                {
                    if (m_EdgeSprings.ContainsKey(toEdge.ID))
                        existingSpring = m_EdgeSprings[toEdge.ID];
                }

                if (existingSpring != null)
                    return new Spring(existingSpring.point2, existingSpring.point1, 0.0f, 0.0f);
                m_EdgeSprings[iEdge.ID] = new Spring(GetPoint(iEdge.Source), GetPoint(iEdge.Target), length, Stiffness);
            }
            return m_EdgeSprings[iEdge.ID];
        }

        public bool Calculate(Graph notused, float deltaTime)
        {
            if (FixedIterations > 0)
            {
				bool updated = false;
                for (int i = 0; i < FixedIterations; ++i)
                    updated |= Tick(deltaTime);
				return updated;
            }

            return Tick(deltaTime);
        }

        public Point GetPoint(Node iNode)
        {
            if (!m_NodePoints.ContainsKey(iNode.name))
                m_NodePoints[iNode.name] = new Point(Vector2.zero, Vector2.zero, iNode);
            return m_NodePoints[iNode.name];
        }

        private void ApplyCoulombsLaw()
        {
            foreach (Node n1 in graph.nodes)
            {
                Point point1 = GetPoint(n1);
                foreach (Node n2 in graph.nodes)
                {
                    Point point2 = GetPoint(n2);
                    if (point1 != point2)
                    {
                        Vector2 d = point1.position - point2.position;
                        float distance = d.magnitude + 0.1f;
                        Vector2 direction = d.normalized;
                        if (n1.pinned && n2.pinned)
                        {
                            point1.ApplyForce(direction * 0.0f);
                            point2.ApplyForce(direction * 0.0f);
                        }
                        else if (n1.pinned)
                        {
                            point1.ApplyForce(direction * 0.0f);
                            point2.ApplyForce(direction * Repulsion / (distance * -1.0f));
                        }
                        else if (n2.pinned)
                        {
                            point1.ApplyForce(direction * Repulsion / distance);
                            point2.ApplyForce(direction * 0.0f);
                        }
                        else
                        {
                            point1.ApplyForce(direction * Repulsion / (distance * 0.5f));
                            point2.ApplyForce(direction * Repulsion / (distance * -0.5f));
                        }
                    }
                }
            }
        }

        private void ApplyHookesLaw()
        {
            foreach (Edge e in graph.edges)
            {
                Spring spring = GetSpring(e);
                Vector2 d = spring.point2.position - spring.point1.position;
                float displacement = spring.Length - d.magnitude;
                Vector2 direction = d.normalized;

                if (spring.point1.node.pinned && spring.point2.node.pinned)
                {
                    spring.point1.ApplyForce(direction * 0.0f);
                    spring.point2.ApplyForce(direction * 0.0f);
                }
                else if (spring.point1.node.pinned)
                {
                    spring.point1.ApplyForce(direction * 0.0f);
                    spring.point2.ApplyForce(direction * (spring.K * displacement));
                }
                else if (spring.point2.node.pinned)
                {
                    spring.point1.ApplyForce(direction * (spring.K * displacement * -1.0f));
                    spring.point2.ApplyForce(direction * 0.0f);
                }
                else
                {
                    spring.point1.ApplyForce(direction * (spring.K * displacement * -0.5f));
                    spring.point2.ApplyForce(direction * (spring.K * displacement * 0.5f));
                }
            }
        }

        private void UpdateVelocity(float iTimeStep)
        {
            foreach (Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                point.velocity = point.velocity + point.acceleration * iTimeStep;
                point.velocity = point.velocity * Damping;
                point.acceleration = Vector2.zero;
            }
		}

        private bool UpdatePosition(float iTimeStep)
        {
			bool updated = false;
			foreach (Node n in graph.nodes)
            {
                Point point = GetPoint(n);
				var op = point.position;
                point.position += point.velocity * iTimeStep;
				updated |= op != point.position;
            }
			return updated;
		}

		private float GetTotalEnergy()
        {
            float energy = 0.0f;
            foreach (Node n in graph.nodes)
            {
                Point point = GetPoint(n);
                float speed = point.velocity.magnitude;
                energy += 0.5f * point.mass * speed * speed;
            }
            return energy;
        }

        private bool Tick(float timeStep)
        {
            ApplyCoulombsLaw();
            ApplyHookesLaw();
            UpdateVelocity(timeStep);
            bool updated = UpdatePosition(timeStep);
            WithinThreashold = GetTotalEnergy() < Threadshold;
			return updated;
        }
    }
}
