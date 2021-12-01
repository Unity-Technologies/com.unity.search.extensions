## Queries
### all_scene_objects
`h: is:object`

**Search Table:** **ID** (`ID`), **path** (`path`), **choice** (`#choice`), **color** (`#color`), **number** (`#number`), **valid** (`#valid`)

### custom_scene_objects
`h: ref=select{find:Assets/Scripts/**/*.cs, path}`


List scene objects that use a project custom script.

**Search Table:** **Name** (`path`), **Type** (`type`)

### scene_files
`*.unity`


### table_loc
`select{where{p:*.* -a:packages, @loc>0}, name, @loc}`

**Search Table:** **name** (`name`), **loc** (`loc`)

### table_materials
`t:material`


Show materials in a search table.

**Search Table:** **Value** (`Value`), **m_Shader** (`#m_Shader`)

## Expressions
### ex0_count
`count{a:assets}`


Count the amount of assets in the project.


### ex1_count_prefab_usage
`sort{select{p: t:prefab *.prefab, @path, count{a:sceneIndex ref:@path} as count}, @count, "desc"} `


Count prefab usage in scenes and sort by highest usage.


### ex2_count_prefab_usage
`sort{select{p: t:prefab *.prefab, @path, count{a:sceneIndex ref:@path} as count}, @count, "desc"} `


Count prefab usage in scenes and sort by highest usage.


### ex3_count_by_types
`sort{count{...groupby{a:assets, @type}}, @value, desc} `


Sort and count all asset types



### ex4_biggest_meshes
`sort{select{h: t:mesh, @path, @vertices}, @vertices, desc} `


Sort all mesh by their vertex count.


### ex5_biggest_meshes
`first{sort{h: t:mesh, @vertices, desc}} `


Find the mesh with the most vertices.


### ex6_total_vertices
`sum{select{h:t:mesh, @vertices}} `


Report the total vertices in the scene.


### ex7_mesh_usage
`ref=select{p:t:mesh, @path} `


Find all assets referencing a mesh.


### ex8_group_by_types
`...groupby{a:assets size>1e4, @type}`


## Tools
### light_explorer
`h:t=Light`


Light Explorer

**Search Table:** **Enabled** (`enabled`), **Name** (`Name`), **m_Type** (`#m_Type`), **m_Shape** (`#m_Shape`), **m_Lightmapping** (`#m_Lightmapping`), **m_Color** (`#m_Color`), **m_Intensity** (`#m_Intensity`), **m_Shadows.m_Type** (`#m_Shadows.m_Type`)

### light_probe_explorer
`t:LightProbe`

**Search Table:** **Enabled** (`enabled`), **Name** (`Name`)

### reflection_probe_explorer
`t:reflectionprobe`

**Search Table:** **Enabled** (`enabled`), **Name** (`Name`), **m_Mode** (`#m_Mode`), **m_HDR** (`#m_HDR`), **m_Resolution** (`#m_Resolution`), **m_ShadowDistance** (`#m_ShadowDistance`), **m_NearClip** (`#m_NearClip`), **m_FarClip** (`#m_FarClip`)

