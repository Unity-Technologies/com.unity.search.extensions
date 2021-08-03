using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UnityEditor.Search
{
    class OrganicLayout : IGraphLayout
    {
        //protected Graph m_Graph;

        /// <summary>
        /// The force constant by which the attractive forces are divided and the
        /// repulsive forces are multiple by the square of. The value equates to the
        /// average radius there is of free space around each node. Default is 50.
        /// </summary>
        protected double m_ForceConstant = 200;

        /// <summary>
        /// Cache of m_ForceConstant^2 for performance.
        /// </summary>
        protected double m_ForceConstantSquared = 0;

        /// <summary>
        /// Minimal distance limit. Default is 2. Prevents of
        /// dividing by zero.
        /// </summary>
        protected double m_MinDistanceLimit = 5.2;

        /// <summary>
        /// Cached version of minDistanceLimit squared.
        /// </summary>
        protected double m_MinDistanceLimitSquared = 0;

        /// <summary>
        /// Start value of temperature. Default is 200.
        /// </summary>
        protected double m_InitialTemp = 200;

        /// <summary>
        /// Temperature to limit displacement at later stages of layout.
        /// </summary>
        protected double m_Temperature = 0;

        /// <summary>
        /// Total number of iterations to run the layout though.
        /// </summary>
        protected int m_MaxIterations = 20000;

        /// <summary>
        /// Current iteration count.
        /// </summary>
        protected int m_Iteration = 0;

        /// <summary>
        /// An array of all vertices to be laid out.
        /// </summary>
        protected Node[] m_VertexArray;

        /// <summary>
        /// An array of locally stored X co-ordinate displacements for the vertices.
        /// </summary>
        protected double[] m_DispX;

        /// <summary>
        /// An array of locally stored Y co-ordinate displacements for the vertices.
        /// </summary>
        protected double[] m_DispY;

        /// <summary>
        /// An array of locally stored co-ordinate positions for the vertices.
        /// </summary>
        protected double[][] m_CellLocation;

        /// <summary>
        /// The approximate radius of each cell, nodes only.
        /// </summary>
        protected double[] m_Radius;

        /// <summary>
        /// The approximate radius squared of each cell, nodes only.
        /// </summary>
        protected double[] m_RadiusSquared;

        /// <summary>
        /// Array of booleans representing the movable states of the vertices.
        /// </summary>
        protected bool[] m_IsMoveable;

        /// <summary>
        /// Local copy of cell neighbors.
        /// </summary>
        protected int[][] m_Neighbours;

        /// <summary>
        /// Boolean flag that specifies if the layout is allowed to run. If this is
        /// set to false, then the layout exits in the following iteration.
        /// </summary>
        protected bool m_AllowedToRun = true;

        /// <summary>
        /// Maps from vertices to indices.
        /// </summary>
        protected Dictionary<object, int> m_Indices = new Dictionary<object, int>();

        /// <summary>
        /// Flag to stop a running layout run.
        /// </summary>
        public bool isAllowedToRun
        {
            get { return m_AllowedToRun; }
            set { m_AllowedToRun = value; }
        }

        /// <summary>
        /// Returns true if the given cell should be ignored by the layout algorithm.
        /// This implementation returns false if the cell is a vertex and has at least
        /// one connected edge.
        /// </summary>
        /// <param name="cell">Object that represents the cell.</param>
        /// <returns>Returns true if the given cell should be ignored.</returns>
        public bool IsCellIgnored(object cell)
        {
            /*
            return !graph.Model.IsVertex(cell) ||
                graph.Model.GetEdgeCount(cell) == 0;
                */
            return false;
        }

        /// <summary>
        /// Maximum number of iterations.
        /// </summary>
        public int maxIterations
        {
            get { return m_MaxIterations; }
            set { m_MaxIterations = value; }
        }

        /// <summary>
        /// Force constant to be used for the springs.
        /// </summary>
        public double mForceConstant
        {
            get { return m_ForceConstant; }
            set { m_ForceConstant = value; }
        }

        /// <summary>
        /// Minimum distance between nodes.
        /// </summary>
        public double minDistanceLimit
        {
            get { return m_MinDistanceLimit; }
            set { m_MinDistanceLimit = value; }
        }

        /// <summary>
        /// Initial temperature.
        /// </summary>
        public double initialTemp
        {
            get { return m_InitialTemp; }
            set { m_InitialTemp = value; }
        }

        /// <summary>
        /// Reduces the temperature of the layout from an initial setting in a linear
        /// fashion to zero.
        /// </summary>
        protected void ReduceTemperature()
        {
            m_Temperature = m_InitialTemp * (1.0 - m_Iteration / (double)m_MaxIterations);
        }

        public bool Animated
        {
            get { return false; }
        }

        /// <summary>
        /// Executes the fast organic layout.
        /// </summary>
        public bool Calculate(Graph graph, float timeStep)
        {
			m_VertexArray = graph.nodes.ToArray();
            int n = m_VertexArray.Length;

            m_DispX = new double[n];
            m_DispY = new double[n];
            m_CellLocation = new double[n][];
            m_IsMoveable = new bool[n];
            m_Neighbours = new int[n][];
            m_Radius = new double[n];
            m_RadiusSquared = new double[n];

            m_MinDistanceLimitSquared = m_MinDistanceLimit * m_MinDistanceLimit;

            if (m_ForceConstant < 0.001)
            {
                m_ForceConstant = 0.001;
            }

            m_ForceConstantSquared = m_ForceConstant * m_ForceConstant;

            // Create a map of vertices first. This is required for the array of
            // arrays called neighbors which holds, for each vertex, a list of
            // ints which represents the neighbors cells to that vertex as
            // the indices into vertexArray
            for (int i = 0; i < m_VertexArray.Length; i++)
            {
                var vertex = m_VertexArray[i];
                m_CellLocation[i] = new double[2];

                // Set up the mapping from array indices to cells
                m_Indices[vertex] = i;
                var bounds = vertex.rect;

                // Set the X,Y value of the internal version of the cell to
                // the center point of the vertex for better positioning
                double width = bounds.width;
                double height = bounds.height;

                // Randomize (0, 0) locations
                double x = bounds.x;
                double y = bounds.y;

                m_CellLocation[i][0] = x + width / 2.0;
                m_CellLocation[i][1] = y + height / 2.0;

                m_Radius[i] = Math.Max(width, height);
                m_RadiusSquared[i] = m_Radius[i] * m_Radius[i];
            }

            for (int i = 0; i < n; i++)
            {
                m_DispX[i] = 0;
                m_DispY[i] = 0;
                m_IsMoveable[i] = !m_VertexArray[i].pinned;

                // Get lists of neighbors to all vertices, translate the cells
                // obtained in indices into vertexArray and store as an array
                // against the original cell index
                var cells = graph.GetNeighbors(m_VertexArray[i].id);

                m_Neighbours[i] = new int[cells.Count];

                for (int j = 0; j < cells.Count; j++)
                {
                    int index = m_Indices[cells[j]];

                    // Check the connected cell in part of the vertex list to be
                    // acted on by this layout
                    if (index != -1)
                    {
                        m_Neighbours[i][j] = index;
                    }
                    // Else if index of the other cell doesn't correspond to
                    // any cell listed to be acted upon in this layout. Set
                    // the index to the value of this vertex (a dummy self-loop)
                    // so the attraction force of the edge is not calculated
                    else
                    {
                        m_Neighbours[i][j] = i;
                    }
                }
            }
            m_Temperature = m_InitialTemp;

            // If max number of iterations has not been set, guess it
            if (m_MaxIterations == 0)
            {
                m_MaxIterations = (int)(20 * Math.Sqrt(n));
            }

            // Main iteration loop
            for (m_Iteration = 0; m_Iteration < m_MaxIterations; m_Iteration++)
            {
                if (!m_AllowedToRun)
                    return false;

                // Calculate repulsive forces on all vertices
                CalcRepulsion();

                // Calculate attractive forces through edges
                CalcAttraction();

                CalcPositions();
                ReduceTemperature();
            }

            // Moved cell location back to top-left from center locations used in
            // algorithm

            float minx = float.NaN;
            float miny = float.NaN;

            for (int i = 0; i < m_VertexArray.Length; i++)
            {
                var vertex = m_VertexArray[i];
				if (m_Neighbours[i].Length == 0)
					continue;
                var geo = vertex.rect;
                m_CellLocation[i][0] -= geo.width / 2.0;
                m_CellLocation[i][1] -= geo.height / 2.0;

                vertex.SetPosition((float)m_CellLocation[i][0], (float)m_CellLocation[i][1]);

                minx = float.IsNaN(minx) ? geo.x : Math.Min(minx, geo.x);
                miny = float.IsNaN(miny) ? geo.y : Math.Min(miny, geo.y);
            }

            // Modifies the cloned geometries in-place. Not needed
            // to clone the geometries again as we're in the same
            // undoable change.
            if (!float.IsNaN(minx) || !float.IsNaN(miny))
            {
                foreach (var vertex in m_VertexArray)
                {
                    var geo = vertex.rect;

                    if (float.IsNaN(minx))
                        geo.x -= minx - 1;

                    if (float.IsNaN(miny))
                        geo.y -= miny - 1;
                    vertex.rect = geo;
                }
            }

			return false;
        }

        /// <summary>
        /// Takes the displacements calculated for each cell and applies them to the
        /// local cache of cell positions. Limits the displacement to the current
        /// temperature.
        /// </summary>
        protected void CalcPositions()
        {
            for (int index = 0; index < m_VertexArray.Length; index++)
            {
                if (m_IsMoveable[index])
                {
                    // Get the distance of displacement for this node for this
                    // iteration
                    double deltaLength = Math.Sqrt(m_DispX[index] * m_DispX[index]
                            + m_DispY[index] * m_DispY[index]);

                    if (deltaLength < 0.001)
                    {
                        deltaLength = 0.001;
                    }

                    // Scale down by the current temperature if less than the
                    // displacement distance
                    double newXDisp = m_DispX[index] / deltaLength
                        * Math.Min(deltaLength, m_Temperature);
                    double newYDisp = m_DispY[index] / deltaLength
                        * Math.Min(deltaLength, m_Temperature);

                    // reset displacements
                    m_DispX[index] = 0;
                    m_DispY[index] = 0;

                    // Update the cached cell locations
                    m_CellLocation[index][0] += newXDisp;
                    m_CellLocation[index][1] += newYDisp;
                }
            }
        }

        /// <summary>
        /// Calculates the attractive forces between all laid out nodes linked by
        /// edges
        /// </summary>
        protected void CalcAttraction()
        {
            // Check the neighbors of each vertex and calculate the attractive
            // force of the edge connecting them
            for (int i = 0; i < m_VertexArray.Length; i++)
            {
                for (int k = 0; k < m_Neighbours[i].Length; k++)
                {
                    // Get the index of the other cell in the vertex array
                    int j = m_Neighbours[i][k];

                    // Do not proceed self-loops
                    if (i != j && i < m_CellLocation.Length && j < m_CellLocation.Length)
                    {
                        double xDelta = m_CellLocation[i][0] - m_CellLocation[j][0];
                        double yDelta = m_CellLocation[i][1] - m_CellLocation[j][1];

                        // The distance between the nodes
                        double deltaLengthSquared = xDelta * xDelta + yDelta
                            * yDelta - m_RadiusSquared[i] - m_RadiusSquared[j];

                        if (deltaLengthSquared < m_MinDistanceLimitSquared)
                        {
                            deltaLengthSquared = m_MinDistanceLimitSquared;
                        }

                        double deltaLength = Math.Sqrt(deltaLengthSquared);
                        double force = (deltaLengthSquared) / m_ForceConstant;

                        double displacementX = (xDelta / deltaLength) * force;
                        double displacementY = (yDelta / deltaLength) * force;

                        if (m_IsMoveable[i])
                        {
                            m_DispX[i] -= displacementX;
                            m_DispY[i] -= displacementY;
                        }

                        if (m_IsMoveable[j])
                        {
                            m_DispX[j] += displacementX;
                            m_DispY[j] += displacementY;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the repulsive forces between all laid out nodes
        /// </summary>
        protected void CalcRepulsion()
        {
            int vertexCount = m_VertexArray.Length;

            for (int i = 0; i < vertexCount; i++)
            {
                for (int j = i; j < vertexCount; j++)
                {
                    // Exits if the layout is no longer allowed to run
                    if (!m_AllowedToRun)
                    {
                        return;
                    }

                    if (j != i)
                    {
                        double xDelta = m_CellLocation[i][0] - m_CellLocation[j][0];
                        double yDelta = m_CellLocation[i][1] - m_CellLocation[j][1];

                        if (xDelta == 0)
                        {
                            xDelta = 0.01 + Random.value;
                        }

                        if (yDelta == 0)
                        {
                            yDelta = 0.01 + Random.value;
                        }

                        // Distance between nodes
                        double deltaLength = Math.Sqrt((xDelta * xDelta)
                                + (yDelta * yDelta));

                        double deltaLengthWithRadius = deltaLength - m_Radius[i]
                            - m_Radius[j];

                        if (deltaLengthWithRadius < m_MinDistanceLimit)
                        {
                            deltaLengthWithRadius = m_MinDistanceLimit;
                        }

                        double force = m_ForceConstantSquared / deltaLengthWithRadius;

                        double displacementX = (xDelta / deltaLength) * force;
                        double displacementY = (yDelta / deltaLength) * force;

                        if (m_IsMoveable[i])
                        {
                            m_DispX[i] += displacementX;
                            m_DispY[i] += displacementY;
                        }

                        if (m_IsMoveable[j])
                        {
                            m_DispX[j] -= displacementX;
                            m_DispY[j] -= displacementY;
                        }
                    }
                }
            }
        }
    }
}
