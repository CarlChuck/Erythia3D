using UnityEngine;
using System.Collections.Generic;

public class WorkBench : MonoBehaviour
{
    #region Private Fields
    [SerializeField] private List<Recipe> recipes = new List<Recipe>();
    [SerializeField] private int workbenchType;
    #endregion

    #region Public Properties
    public List<Recipe> Recipes
    {
        get { return new List<Recipe>(recipes); }
    }
    #endregion

    #region Public Methods
    public void InitializeRecipes(List<Recipe> initialRecipes)
    {
        recipes.Clear();
        if (initialRecipes != null)
        {
            recipes.AddRange(initialRecipes);
        }
    }
    public void AddRecipe(Recipe recipe)
    {
        if (recipe != null && !recipes.Contains(recipe))
        {
            recipes.Add(recipe);
        }
    }
    public int GetWorkbenchType()
    {
        return workbenchType;
    }
    public void SetWorkbenchType(int newWorkbenchType)
    {
        workbenchType = newWorkbenchType;
    }
    #endregion
}
