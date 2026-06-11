import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/product.dart';

class SearchScreen extends StatefulWidget {
  const SearchScreen({super.key});

  @override
  State<SearchScreen> createState() => _SearchScreenState();
}

class _SearchScreenState extends State<SearchScreen> {
  final _ctrl = TextEditingController();
  final _api = ApiClient();
  List<ProductSearchResult> _results = [];
  bool _loading = false;
  bool _searched = false;

  Future<void> _search(String q) async {
    if (q.trim().length < 2) {
      setState(() { _results = []; _searched = false; });
      return;
    }
    setState(() { _loading = true; _searched = true; });
    try {
      final data = await _api.getList('/products/search', params: {'q': q.trim()});
      setState(() => _results = data.map((j) => ProductSearchResult.fromJson(j)).toList());
    } catch (_) {
      setState(() => _results = []);
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _showAddProduct() async {
    final created = await showModalBottomSheet<ProductSearchResult>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (_) => _AddProductSheet(
        api: _api,
        initialName: _ctrl.text.trim(),
      ),
    );
    if (created != null && mounted) {
      context.push('/compare/${created.id}');
    }
  }

  @override
  void dispose() {
    _ctrl.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: TextField(
          controller: _ctrl,
          autofocus: true,
          style: const TextStyle(color: Colors.white),
          cursorColor: Colors.white,
          decoration: const InputDecoration(
            hintText: 'Buscar productos...',
            hintStyle: TextStyle(color: Colors.white70),
            fillColor: Colors.transparent,
            border: InputBorder.none,
          ),
          onChanged: _search,
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.add_circle_outline, color: Colors.white),
            tooltip: 'Agregar producto',
            onPressed: _showAddProduct,
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _results.isNotEmpty
              ? ListView.separated(
                  itemCount: _results.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (_, i) => _buildResultTile(_results[i]),
                )
              : _buildEmptyState(),
    );
  }

  Widget _buildEmptyState() {
    if (!_searched) {
      return Center(child: Text('Escribe para buscar', style: TextStyle(color: Colors.grey[500])));
    }
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.search_off, size: 56, color: Colors.grey[300]),
            const SizedBox(height: 12),
            Text(
              '"${_ctrl.text.trim()}" no está en la base de datos.',
              textAlign: TextAlign.center,
              style: TextStyle(color: Colors.grey[600]),
            ),
            const SizedBox(height: 20),
            ElevatedButton.icon(
              onPressed: _showAddProduct,
              icon: const Icon(Icons.add),
              label: const Text('Agregar producto manualmente'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildResultTile(ProductSearchResult p) {
    return ListTile(
      leading: Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: const Color(0xFFE1F5EE),
          borderRadius: BorderRadius.circular(8),
        ),
        child: const Icon(Icons.inventory_2_outlined, color: Color(0xFF1D9E75)),
      ),
      title: Text(p.name, style: const TextStyle(fontSize: 15)),
      subtitle: Text(p.brand ?? p.category ?? '', style: const TextStyle(fontSize: 13)),
      trailing: p.lowestPrice != null
          ? Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text('₡${p.lowestPrice!.toStringAsFixed(0)}',
                    style: const TextStyle(color: Color(0xFF1D9E75), fontWeight: FontWeight.w600)),
                Text(p.lowestPriceStore ?? '', style: const TextStyle(fontSize: 11, color: Colors.grey)),
              ],
            )
          : null,
      onTap: () => context.push('/compare/${p.id}'),
    );
  }
}

class _AddProductSheet extends StatefulWidget {
  final ApiClient api;
  final String initialName;

  const _AddProductSheet({required this.api, required this.initialName});

  @override
  State<_AddProductSheet> createState() => _AddProductSheetState();
}

class _AddProductSheetState extends State<_AddProductSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameCtrl;
  final _brandCtrl = TextEditingController();
  final _categoryCtrl = TextEditingController();
  final _barcodeCtrl = TextEditingController();
  bool _saving = false;
  String? _error;

  // Common CR product categories
  static const _categories = [
    'Lácteos', 'Granos', 'Aceites', 'Panadería', 'Salsas',
    'Enlatados', 'Abarrotes', 'Café', 'Pastas', 'Limpieza',
    'Frescos', 'Carnes', 'Bebidas', 'Snacks', 'Otros',
  ];

  @override
  void initState() {
    super.initState();
    _nameCtrl = TextEditingController(text: widget.initialName);
  }

  @override
  void dispose() {
    _nameCtrl.dispose();
    _brandCtrl.dispose();
    _categoryCtrl.dispose();
    _barcodeCtrl.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() { _saving = true; _error = null; });
    try {
      final json = await widget.api.post('/products', {
        'name': _nameCtrl.text.trim(),
        if (_brandCtrl.text.isNotEmpty) 'brand': _brandCtrl.text.trim(),
        if (_categoryCtrl.text.isNotEmpty) 'category': _categoryCtrl.text.trim(),
        if (_barcodeCtrl.text.isNotEmpty) 'barcode': _barcodeCtrl.text.trim(),
      });
      if (mounted) {
        Navigator.pop(context, ProductSearchResult(
          id: json['id'],
          barcode: json['barcode'],
          name: json['name'],
          brand: json['brand'],
          category: json['category'],
          imageUrl: json['imageUrl'],
          lowestPrice: null,
          lowestPriceStore: null,
        ));
      }
    } catch (_) {
      setState(() => _error = 'Error al guardar el producto. Intenta de nuevo.');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const SizedBox(height: 12),
            Center(child: Container(width: 36, height: 4, decoration: BoxDecoration(color: Colors.grey[300], borderRadius: BorderRadius.circular(2)))),
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 4),
              child: Text('Agregar producto', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
            ),
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 0, 16, 12),
              child: Text('El producto quedará disponible para que todos puedan reportar precios.',
                  style: TextStyle(fontSize: 13, color: Colors.grey)),
            ),
            const Divider(height: 1),
            Padding(
              padding: const EdgeInsets.all(16),
              child: Form(
                key: _formKey,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    TextFormField(
                      controller: _nameCtrl,
                      autofocus: widget.initialName.isEmpty,
                      textCapitalization: TextCapitalization.sentences,
                      decoration: InputDecoration(
                        labelText: 'Nombre del producto *',
                        hintText: 'ej. Leche Dos Pinos Entera 1L',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                      ),
                      validator: (v) => (v == null || v.trim().isEmpty) ? 'El nombre es requerido' : null,
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _brandCtrl,
                      textCapitalization: TextCapitalization.words,
                      decoration: InputDecoration(
                        labelText: 'Marca',
                        hintText: 'ej. Dos Pinos',
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Autocomplete<String>(
                      optionsBuilder: (value) => value.text.isEmpty
                          ? _categories
                          : _categories.where((c) => c.toLowerCase().contains(value.text.toLowerCase())),
                      onSelected: (v) => _categoryCtrl.text = v,
                      fieldViewBuilder: (_, ctrl, focusNode, onSubmit) => TextFormField(
                        controller: ctrl,
                        focusNode: focusNode,
                        textCapitalization: TextCapitalization.sentences,
                        decoration: InputDecoration(
                          labelText: 'Categoría',
                          hintText: 'ej. Lácteos',
                          border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                          contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                        ),
                        onChanged: (v) => _categoryCtrl.text = v,
                        onFieldSubmitted: (_) => onSubmit(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: _barcodeCtrl,
                      keyboardType: TextInputType.number,
                      decoration: InputDecoration(
                        labelText: 'Código de barras (opcional)',
                        hintText: 'EAN-13 / UPC-A',
                        prefixIcon: const Icon(Icons.barcode_reader),
                        border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
                        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                      ),
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 8),
                      Text(_error!, style: const TextStyle(color: Colors.red, fontSize: 13)),
                    ],
                    const SizedBox(height: 20),
                    SizedBox(
                      height: 48,
                      child: ElevatedButton(
                        onPressed: _saving ? null : _save,
                        child: _saving
                            ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                            : const Text('Guardar producto'),
                      ),
                    ),
                    const SizedBox(height: 16),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
