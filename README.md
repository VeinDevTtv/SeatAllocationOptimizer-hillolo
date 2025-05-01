# Maximizing Airline Revenue: A Systematic Approach
> 3andek chi company dial tyarat? Bghiti tdir revenue? Weli m3ana f **Hilolo Airlines Simulator** — hada l'code dial Hilolo, li z3ma fakkar f seating optimization w nsaa l'user. W ana vein mrid f rasi hh

![Airplane Seating Chart Meme](https://images2.memedroid.com/images/UPLOADED54/52f66fe589464.jpeg)

This application optimizes airline seating arrangements to maximize revenue while considering passenger preferences and family groupings.

**Current Status & Known Limitations:**
*   The core logic for revenue maximization and basic seating allocation is implemented.
*   Bug fixes related to family placement edge cases have been applied. (walakin daba okay… I think 🫣)
*   Window seat preference handling has been added.
*   Input parsing is more robust.
*   Unit and integration tests have been added.
*   **Refactored:** The core `SeatingAllocator` logic has been refactored to improve efficiency and robustness, replacing the previous iterative row-by-row placement attempt with a strategy that evaluates available blocks across the entire plane for each group.
*   **Limitation:** The family seating rule ensures children are adjacent to adults within their assigned block but assumes sequential placement based on input order and does not explicitly handle aisle separation.
*   **Limitation:** The allocation loop prioritizes revenue but might be inefficient in scenarios where large, high-revenue groups cannot fit until later rows.

## Program Structure Overview ft. Hilolo's gay Design

The program utilizes several C# classes:

*   **`Passenger`**: Manages individual passenger details (Adult/Child, Revenue, Window Preference).
*   **`Family`**: Groups passengers by family ID, managing collective revenue and seat needs.
*   **`BoardingGroup`**: A wrapper for individuals or families for uniform handling.
*   **`BoardingGroupComparator`**: Prioritizes groups based on higher revenue per seat, then fewer seats needed.
*   **`DataParser`**: Parses passenger data from a CSV file.
*   **`SeatingAllocator`**: Performs the main seating allocation logic.

## Key Features & Logic

### 1. Input Data Parsing
CSV format li mafih hata checkbox.
The program reads passenger data from `SeatAllocationOptimizer.Main/Data/input.txt`. The expected CSV format is:

```
ID,Type,Revenue,FamilyID,WindowPref
```

*   **`ID`**: Passenger identifier (currently not used in core logic).
*   **`Type`**: `Adult` or `Child` (case-insensitive).
*   **`Revenue`**: The revenue associated with the passenger (numeric).
*   **`FamilyID`**: An identifier to group families. Use `-` for individual passengers.
*   **`WindowPref`**: `Yes` (case-insensitive) if the passenger prefers a window seat, any other value (or empty) means no preference.

The parser handles malformed lines, empty lines, and file errors gracefully by skipping problematic entries and logging warnings.

### 2. Sorting and Prioritization
Boarding groups (individuals and families) are placed into a `PriorityQueue` using `BoardingGroupComparator`. This ensures groups with higher revenue per seat are processed first. If revenue per seat is equal, smaller groups (requiring fewer seats) are prioritized.

### 3. Dynamic Seating Strategy
The `SeatingAllocator` processes the prioritized boarding groups:
*   It maintains a dynamic list of available contiguous seat blocks for each row.
*   For each group from the priority queue, it searches across *all* available blocks in *all* rows to find potential valid placements.
*   A placement is considered valid if the block is large enough and satisfies family placement rules (see below).
*   **Window Preference**: The allocator prioritizes placing groups wanting a window seat into a block that includes a window (first or last seat of the row). If multiple preferred blocks are found, the first one encountered is typically chosen. If no preferred block is available, it will select the first available valid non-window block as a fallback.
*   **Family Handling**: Families are always seated together in a contiguous block. The `IsFamilyPlacementValid` check ensures that if a family includes children (`Type=Child`), every child has an adult from the same family seated immediately to their left or right *within the assigned block* (based on the sequential order of members in the family list). (See limitations above regarding aisles).
*   Once a group is placed, the list of available blocks is updated, and the group's revenue is added to the total.

### 4. Output
After allocation, the program prints:
*   A visualization of the seating map, showing which group occupies which seat.
*   The total calculated revenue from seated passengers.
*   Information about any groups that could not be seated.
*   Execution time and memory usage statistics.

## Running the Application

### Prerequisites
*   .NET 8 SDK
*   Docker (Optional, for containerized execution)

### Execution

1.  **Clone the repository.**
2.  **Navigate to the main project directory:**
    ```bash
    cd SeatAllocationOptimizer.Main
    ```
3.  **Run using dotnet:**
    ```bash
    # Use default plane dimensions (33 rows, 6 seats/row)
    dotnet run

    # Specify custom dimensions (e.g., 10 rows, 4 seats/row)
    dotnet run -- 10 4
    ```
    The input data will be read from `Data/input.txt`.

## Docker Container Management

Use the `Dockerfile` located within the `SeatAllocationOptimizer.Main` directory.

### 1. Building the Docker Image

Navigate to the `SeatAllocationOptimizer.Main` directory and run:

```bash
cd SeatAllocationOptimizer.Main/
docker build -t seat-allocation-optimizer .
cd .. 
```
*(Tag name `seat-allocation-optimizer` is suggested)*

### 2. Running the Docker Container

After building the image, run the container:

```bash
# Run interactively to see console output immediately
docker run --rm --name sao-run seat-allocation-optimizer

# Alternatively, run detached and view logs later:
# docker run --name sao-run seat-allocation-optimizer
# docker logs sao-run
```
*(Container name `sao-run` is suggested)*

### 3. Stopping and Removing (if not using `--rm`)

```bash
docker stop sao-run
docker rm sao-run
```

## Development & Testing

*   The solution can be opened in Visual Studio or VS Code.
*   Unit and integration tests are located in the `SeatAllocationOptimizer.Test` project.
*   Run tests using the Visual Studio Test Explorer or via the command line:
    ```bash
    dotnet test
    ```

## Future Considerations / To-Do

*   **Performance Validation:** Test with significantly larger input datasets.
*   **Advanced Family Rules:** Implement more sophisticated family seating logic (e.g., aisle awareness, placement permutations).
*   **Configurable Input Path:** Allow the input file path to be specified via command line.
*   **Logging:** Implement a more formal logging framework instead of `Console.WriteLine`.


## Special Thanks 💖
Hilolo, nta zwin a777, walakin ra ma code ma walo hada, w ana zedt 3lih kter HHHHHHHHHHH

**TL;DR**:
Hilolo wrote a C# airline allocator, but got lost mid-flight.
We still love you ya baby💸✈️❤️
