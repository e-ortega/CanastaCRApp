class Product {
  final String id;
  final String? barcode;
  final String name;
  final String? brand;
  final String? category;
  final String? imageUrl;
  final String? description;

  const Product({
    required this.id,
    this.barcode,
    required this.name,
    this.brand,
    this.category,
    this.imageUrl,
    this.description,
  });

  factory Product.fromJson(Map<String, dynamic> j) => Product(
        id: j['id'],
        barcode: j['barcode'],
        name: j['name'],
        brand: j['brand'],
        category: j['category'],
        imageUrl: j['imageUrl'],
        description: j['description'],
      );
}

class ProductSearchResult {
  final String id;
  final String? barcode;
  final String name;
  final String? brand;
  final String? category;
  final String? imageUrl;
  final double? lowestPrice;
  final String? lowestPriceStore;

  const ProductSearchResult({
    required this.id,
    this.barcode,
    required this.name,
    this.brand,
    this.category,
    this.imageUrl,
    this.lowestPrice,
    this.lowestPriceStore,
  });

  factory ProductSearchResult.fromJson(Map<String, dynamic> j) => ProductSearchResult(
        id: j['id'],
        barcode: j['barcode'],
        name: j['name'],
        brand: j['brand'],
        category: j['category'],
        imageUrl: j['imageUrl'],
        lowestPrice: (j['lowestPrice'] as num?)?.toDouble(),
        lowestPriceStore: j['lowestPriceStore'],
      );
}
