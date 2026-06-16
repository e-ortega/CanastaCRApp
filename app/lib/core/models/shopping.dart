class ShoppingList {
  final String id;
  final String name;
  final DateTime createdAt;
  final List<ShoppingListItem> items;

  const ShoppingList({
    required this.id,
    required this.name,
    required this.createdAt,
    required this.items,
  });

  factory ShoppingList.fromJson(Map<String, dynamic> j) => ShoppingList(
        id: j['id'],
        name: j['name'],
        createdAt: DateTime.parse(j['createdAt']),
        items: (j['items'] as List).map((i) => ShoppingListItem.fromJson(i)).toList(),
      );

  int get pendingCount => items.where((i) => !i.isPurchased).length;
}

class ShoppingListItem {
  final String id;
  final String productId;
  final String productName;
  final String? productImageUrl;
  final double quantity;
  final String unit;
  final bool isPurchased;

  const ShoppingListItem({
    required this.id,
    required this.productId,
    required this.productName,
    this.productImageUrl,
    required this.quantity,
    required this.unit,
    required this.isPurchased,
  });

  factory ShoppingListItem.fromJson(Map<String, dynamic> j) => ShoppingListItem(
        id: j['id'],
        productId: j['productId'],
        productName: j['productName'],
        productImageUrl: j['productImageUrl'],
        quantity: (j['quantity'] as num).toDouble(),
        unit: j['unit'].toString(),
        isPurchased: j['isPurchased'],
      );
}

class OptimizationResult {
  final String shoppingListId;
  final double totalEstimatedCost;
  final double totalIfSingleStore;
  final double totalSavings;
  final double savingsPercent;
  final List<StoreGroup> storeGroups;

  const OptimizationResult({
    required this.shoppingListId,
    required this.totalEstimatedCost,
    required this.totalIfSingleStore,
    required this.totalSavings,
    required this.savingsPercent,
    required this.storeGroups,
  });

  factory OptimizationResult.fromJson(Map<String, dynamic> j) => OptimizationResult(
        shoppingListId: j['shoppingListId'],
        totalEstimatedCost: (j['totalEstimatedCost'] as num).toDouble(),
        totalIfSingleStore: (j['totalIfSingleStore'] as num).toDouble(),
        totalSavings: (j['totalSavings'] as num).toDouble(),
        savingsPercent: (j['savingsPercent'] as num).toDouble(),
        storeGroups: (j['storeGroups'] as List).map((g) => StoreGroup.fromJson(g)).toList(),
      );
}

class StoreGroup {
  // Null for chain-level scraped prices (no specific physical location) — see
  // docs/ARCHITECTURE.md section 11. storeName/chain are always present either way.
  final String? storeId;
  final String storeName;
  final String chain;
  final double groupTotal;
  final List<OptimizedItem> items;

  const StoreGroup({
    this.storeId,
    required this.storeName,
    required this.chain,
    required this.groupTotal,
    required this.items,
  });

  factory StoreGroup.fromJson(Map<String, dynamic> j) => StoreGroup(
        storeId: j['storeId'],
        storeName: j['storeName'],
        chain: j['chain'].toString(),
        groupTotal: (j['groupTotal'] as num).toDouble(),
        items: (j['items'] as List).map((i) => OptimizedItem.fromJson(i)).toList(),
      );
}

class OptimizedItem {
  final String productId;
  final String productName;
  final double price;
  final double quantity;
  final double lineTotal;
  final double savingsVsHighest;

  const OptimizedItem({
    required this.productId,
    required this.productName,
    required this.price,
    required this.quantity,
    required this.lineTotal,
    required this.savingsVsHighest,
  });

  factory OptimizedItem.fromJson(Map<String, dynamic> j) => OptimizedItem(
        productId: j['productId'],
        productName: j['productName'],
        price: (j['price'] as num).toDouble(),
        quantity: (j['quantity'] as num).toDouble(),
        lineTotal: (j['lineTotal'] as num).toDouble(),
        savingsVsHighest: (j['savingsVsHighest'] as num).toDouble(),
      );
}
